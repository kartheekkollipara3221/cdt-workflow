using System.Globalization;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Platform;
using BBH.CP.Atlas.Service.Platform.Attachments;
using BBH.CP.Atlas.Service.Platform.Audit;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Rules;
using BBH.CP.Atlas.Service.Platform.Sections;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.AspNetCore.Http;

namespace BBH.CP.Atlas.Service.Application
{
    public partial class ConfigSectionService : IAtlasAttachmentSource
    {
        /// <summary>A file picker of the section with its effective state after the rules.</summary>
        private sealed record FilePicker(CPApplicationControl Control, bool Visible, bool Enabled)
        {
            public string Label => Control.Label ?? Control.Name;
        }

        /// <summary>The files of one save: the new files and the stored files that the save removes.</summary>
        private sealed class SaveFiles
        {
            public AtlasAttachmentTable? Table { get; set; }
            public List<AtlasNewAttachment> Added { get; } = [];
            public List<AtlasAttachmentInfo> Removed { get; } = [];
        }

        /// <summary>
        /// Returns the attachment table of the app with its columns, or null when the app has no attachment table.
        /// </summary>
        /// <exception cref="AtlasProblemException">A configuration problem when a necessary column is missing.</exception>
        protected AtlasAttachmentTable? AttachmentTableOf(AtlasAppManifest manifest)
        {
            ArgumentNullException.ThrowIfNull(manifest);
            if (string.IsNullOrWhiteSpace(manifest.AttachmentTable))
                return null;

            List<string> errors = [];
            return AtlasAttachmentTable.Resolve(Services.PlatformContext.Registry.Get(manifest.AttachmentTable), manifest.AttachmentColumns, errors)
                ?? throw AtlasProblemException.Config($"App '{manifest.Code}': {string.Join(" ", errors)}");
        }

        /// <summary>Returns the file pickers of the section, with the state that the rules gave them.</summary>
        private static List<FilePicker> FilePickers(CPApplicationSection section)
        {
            List<FilePicker> pickers = [];
            foreach (CPApplicationControl control in section.Controls ?? [])
            {
                if (AtlasAttachmentRules.IsFilePicker(control))
                    pickers.Add(new FilePicker(control, control.Visible, !control.Disabled));
                foreach (CPApplicationControl child in (control.ChildControls ?? []).Where(AtlasAttachmentRules.IsFilePicker))
                    pickers.Add(new FilePicker(child, control.Visible && child.Visible, !control.Disabled && !child.Disabled));
            }
            return pickers;
        }

        /// <summary>
        /// Gives each visible file picker the stored files of its control. A hidden file picker gets no files.
        /// </summary>
        /// <remarks>The load does not read the content. A file of a disabled file picker is disabled.</remarks>
        private void LoadAttachments(AtlasAppManifest manifest, CPApplicationSection section, int recordId)
        {
            List<FilePicker> pickers = FilePickers(section);
            if (pickers.Count == 0)
                return;

            AtlasAttachmentTable? table = AttachmentTableOf(manifest);
            IReadOnlyList<AtlasAttachmentInfo> files = table == null || recordId <= 0 ? [] : Services.Attachments.List(table, recordId);
            foreach (FilePicker picker in pickers)
            {
                picker.Control.Files = !picker.Visible ? [] : [.. files
                    .Where(x => string.Equals(x.ControlName, picker.Control.Name, StringComparison.OrdinalIgnoreCase))
                    .Select(x => new CPAttachment
                    {
                        ID = x.Id.ToString(CultureInfo.InvariantCulture),
                        Name = x.FileName,
                        Type = picker.Control.Name,
                        Disabled = !picker.Enabled,
                        SizeBytes = x.SizeBytes,
                        CreatedBy = x.CreatedBy ?? (x.CreatedByLanId == null ? null : Services.Directory.DisplayName(x.CreatedByLanId)),
                        CreatedDate = x.CreatedDate == null ? null : AtlasValueConverter.ToWire(x.CreatedDate, DateColumn, Manifest.SystemTimeIsUtc)
                    })];
            }
        }

        /// <summary>The column type for the wire text of a file time.</summary>
        private static readonly AtlasColumn DateColumn = new("CreatedDate", "datetime2", true, null, 27, 7, false, false, false);

        /// <summary>
        /// Reads and checks the new files and the removed files of a save.
        /// </summary>
        /// <remarks>
        /// A file is accepted only for a file picker that is visible and enabled for the user and the state.
        /// The checks are the type, the size, the signature, the number of files and Required.
        /// Each refused file gives an error and the log event Atlas.AttachmentRefused. Nothing is written.
        /// </remarks>
        private void ReadFiles(SaveState state, IFormFileCollection? files)
        {
            List<FilePicker> pickers = FilePickers(state.Section);
            IReadOnlyList<int> removedIds = state.Input.RemovedAttachmentIds ?? [];
            if (pickers.Count == 0 && (files == null || files.Count == 0) && removedIds.Count == 0)
                return;

            SaveFiles result = state.Files;
            result.Table = AttachmentTableOf(state.Manifest);
            if (result.Table == null)
            {
                state.Errors.Add(Error("", "NotAllowed", "This form does not keep files."));
                return;
            }

            Dictionary<string, FilePicker> byName = pickers.ToDictionary(x => x.Control.Name, StringComparer.OrdinalIgnoreCase);
            ReadNewFiles(state, files, byName);
            ReadRemovedFiles(state, removedIds, byName);
            CheckFileCounts(state, pickers);
        }

        private void ReadNewFiles(SaveState state, IFormFileCollection? files, Dictionary<string, FilePicker> byName)
        {
            long total = 0;
            foreach (IFormFile file in files ?? (IEnumerable<IFormFile>)[])
            {
                if (!AtlasAttachmentRules.TrySplitPartName(file.FileName, out string controlName, out string fileName))
                {
                    Refuse(state, "", file.FileName, "InvalidFile", "A file has no control name.");
                    continue;
                }

                if (!byName.TryGetValue(controlName, out FilePicker? picker))
                {
                    Refuse(state, controlName, fileName, "NotAllowed", $"{controlName} is not a file control of this form.");
                    continue;
                }

                if (!picker.Visible || !picker.Enabled)
                {
                    Refuse(state, controlName, fileName, "NotAllowed", $"Files cannot be added to {picker.Label}.");
                    continue;
                }

                if (FileError(picker, fileName, file) is { } error)
                {
                    Refuse(state, picker.Control.Name, fileName, error.Code, error.Message);
                    continue;
                }

                byte[] content = Read(file);
                if (!AtlasAttachmentRules.SignatureMatches(fileName, content))
                {
                    Refuse(state, picker.Control.Name, fileName, "FileSignature",
                        $"{picker.Label}: the content of the file '{fileName}' is not a {AtlasAttachmentRules.Extension(fileName)} file.");
                    continue;
                }

                total += content.Length;
                state.Files.Added.Add(new AtlasNewAttachment(picker.Control.Name, fileName, content));
            }

            if (total > AtlasAttachmentRules.MaxSaveMb * 1024L * 1024L)
                state.Errors.Add(Error("", "SaveSize", AtlasAttachmentRules.SaveSizeText));
        }

        /// <summary>Returns the error of a new file of a file picker before its content is read, or null.</summary>
        private static CPPageError? FileError(FilePicker picker, string fileName, IFormFile file)
        {
            string label = picker.Label;
            IReadOnlyList<string> accept = AtlasAttachmentRules.AcceptOf(picker.Control);
            if (fileName.Length == 0)
                return Error(picker.Control.Name, "InvalidFile", $"{label}: a file has no name.");
            if (fileName.Length > AtlasAttachmentRules.MaxNameLength)
                return Error(picker.Control.Name, "FileName",
                    $"{label}: the file name is longer than {AtlasAttachmentRules.MaxNameLength} characters.");
            if (!accept.Contains(AtlasAttachmentRules.Extension(fileName), StringComparer.OrdinalIgnoreCase))
                return Error(picker.Control.Name, "FileType", AtlasAttachmentRules.TypeText(label, fileName, accept));
            if (file.Length == 0)
                return Error(picker.Control.Name, "FileEmpty", AtlasAttachmentRules.EmptyText(label, fileName));
            if (file.Length > AtlasAttachmentRules.MaxBytesOf(picker.Control))
                return Error(picker.Control.Name, "FileSize", AtlasAttachmentRules.SizeText(label, fileName, AtlasAttachmentRules.MaxMbOf(picker.Control)));
            return null;
        }

        private void ReadRemovedFiles(SaveState state, IReadOnlyList<int> ids, Dictionary<string, FilePicker> byName)
        {
            foreach (int id in ids.Distinct())
            {
                AtlasAttachmentInfo? info = state.IsNew ? null : Services.Attachments.Find(state.Files.Table!, id);
                bool allowed = info != null && info.RecordId == state.Input.ID
                    && byName.TryGetValue(info.ControlName, out FilePicker? picker) && picker.Visible && picker.Enabled;
                if (!allowed)
                {
                    Refuse(state, info?.ControlName ?? "", info?.FileName ?? id.ToString(CultureInfo.InvariantCulture), "NotAllowed",
                        $"The file {id.ToString(CultureInfo.InvariantCulture)} cannot be removed.", pageError: true);
                    continue;
                }

                state.Files.Removed.Add(info!);
            }
        }

        /// <summary>Checks Required, Multiple and MaxFiles of each enabled file picker for the files after the save.</summary>
        private void CheckFileCounts(SaveState state, List<FilePicker> pickers)
        {
            HashSet<string> refused = new(state.Errors.Select(x => x.Field ?? ""), StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<AtlasAttachmentInfo> stored = state.IsNew ? [] : Services.Attachments.List(state.Files.Table!, state.Input.ID);
            foreach (FilePicker picker in pickers.Where(x => x.Visible && x.Enabled && !refused.Contains(x.Control.Name)))
            {
                string name = picker.Control.Name;
                int count = stored.Count(x => Same(x.ControlName, name) && !state.Files.Removed.Any(r => r.Id == x.Id))
                    + state.Files.Added.Count(x => Same(x.ControlName, name));
                int? max = picker.Control.Multiple ? picker.Control.MaxFiles : 1;

                if (count == 0 && picker.Control.Validator?.Required == true)
                    state.Errors.Add(Error(name, "Required", picker.Control.Validator.Message ?? $"{picker.Label} is required."));
                else if (max is > 0 && count > max)
                    state.Errors.Add(Error(name, "MaxFiles", AtlasAttachmentRules.CountText(picker.Label, max.Value)));
            }

            static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private void Refuse(SaveState state, string controlName, string fileName, string code, string message, bool pageError = false)
        {
            state.Errors.Add(Error(pageError ? "" : controlName, code, message));
            AtlasLog.AttachmentRefused(Logger, state.Manifest.Code, Services.User.LanId, state.Input.ID, controlName, fileName, code);
        }

        private static byte[] Read(IFormFile file)
        {
            using MemoryStream memory = new((int)file.Length);
            using Stream stream = file.OpenReadStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        /// <summary>
        /// Writes the removed and the new files of a save in the transaction of the record.
        /// </summary>
        /// <returns>The audit entries: one AttachmentRemove for each removed file and one AttachmentAdd for each new file.</returns>
        /// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException">
        /// Another save removed one of the files after the checks.
        /// </exception>
        private List<(string Event, AtlasAuditChange Change)> WriteFiles(SaveState state, int recordId)
        {
            List<(string Event, AtlasAuditChange Change)> entries = [];
            SaveFiles files = state.Files;
            if (files.Table == null)
                return entries;

            if (files.Removed.Count > 0
                && Services.Attachments.Remove(files.Table, recordId, [.. files.Removed.Select(x => x.Id)], state.Now) != files.Removed.Count)
                throw new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("A file was removed by another save.");

            foreach (AtlasAttachmentInfo removed in files.Removed)
                entries.Add((AtlasAuditEvents.AttachmentRemove, new AtlasAuditChange(removed.ControlName, LabelOf(state, removed.ControlName), removed.FileName, null)));

            foreach (AtlasNewAttachment added in files.Added)
            {
                Services.Attachments.Add(files.Table, recordId, added, state.Now);
                entries.Add((AtlasAuditEvents.AttachmentAdd, new AtlasAuditChange(added.ControlName, LabelOf(state, added.ControlName), null, added.FileName)));
            }

            return entries;
        }

        private static string? LabelOf(SaveState state, string controlName) =>
            FilePickers(state.Section).FirstOrDefault(x => string.Equals(x.Control.Name, controlName, StringComparison.OrdinalIgnoreCase))?.Control.Label;

        /// <summary>
        /// Returns a stored file for a download, after the View check of its record.
        /// </summary>
        /// <remarks>
        /// The content type comes from the stored file name, not from the upload. A file of a file picker that a rule
        /// hides from the user is not found.
        /// </remarks>
        public AtlasAttachmentContent OpenAttachment(int id)
        {
            AtlasAppManifest manifest = Manifest;
            CPAtlasApplicationConfig form = Services.ConfigStore.GetForm(AppCode);
            AtlasAttachmentTable table = AttachmentTableOf(manifest)
                ?? throw AtlasProblemException.NotFound($"App '{manifest.Code}' has no files.");
            AtlasAttachmentInfo info = Services.Attachments.Find(table, id)
                ?? throw AtlasProblemException.NotFound($"File {id} of {manifest.Code} was not found.");

            AtlasTable main = MainTable(manifest);
            Dictionary<string, object?> record = Services.PlatformContext.FindActive(main.FullName, info.RecordId, tracking: false)
                ?? throw AtlasProblemException.NotFound($"File {id} of {manifest.Code} was not found.");
            Services.Access.Demand(manifest, form, AtlasOperation.View, record, info.RecordId);

            CPApplicationSection section = form.Section
                ?? throw AtlasProblemException.Config($"The form of {manifest.Code} has no Section.");
            string? workflowState = AtlasWorkflowSteps.StateOf(section, main, record, isNew: false);
            Services.Rules.ApplyToSection(section, form.Rules, RuleContext(manifest, form, record, workflowState), manifest.RuleMode, manifest.RuleDefault);
            if (!FilePickers(section).Any(x => x.Visible && string.Equals(x.Control.Name, info.ControlName, StringComparison.OrdinalIgnoreCase)))
                throw AtlasProblemException.NotFound($"File {id} of {manifest.Code} was not found.");

            byte[] content = Services.Attachments.ReadContent(table, id)
                ?? throw AtlasProblemException.NotFound($"File {id} of {manifest.Code} was not found.");
            return new AtlasAttachmentContent(info.FileName, AtlasAttachmentRules.ContentTypeOf(info.FileName), content);
        }
    }
}
