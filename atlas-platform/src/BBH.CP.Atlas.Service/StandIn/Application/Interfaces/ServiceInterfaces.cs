// STAND-IN: do not copy to the target.
// Target: Service/Application/Interfaces/*.cs. Same names, same access modifiers, same members.
// IWorkflowService is left out (it uses workflow engine types; release P1 has no workflow).
using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Enumerations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace BBH.CP.Atlas.Service.Application.Interfaces
{
    /// <summary>The service of one app: save, delete, dashboard and audit log.</summary>
    public interface IApplicationService
    {
        /// <summary>The database context of Atlas.</summary>
        CPSharepointContext? Context { get; set; }
        /// <summary>The configuration of the host.</summary>
        IConfiguration? Configuration { get; set; }
        /// <summary>The current user.</summary>
        CPUser? User { get; set; }
        /// <summary>The app code of a current (legacy) app.</summary>
        CPApplicationCode Code { get; set; }
        /// <summary>The code of the parent app, or null.</summary>
        CPApplicationCode? ParentCode { get; set; }
        /// <summary>The form configuration of the app.</summary>
        CPAtlasApplicationConfig? AppConfig { get; set; }
        /// <summary>The dashboard of the app.</summary>
        CPAtlasDashboard? Dashboard { get; set; }
        /// <summary>Saves a record and its files.</summary>
        CPAtlasPage SavePage(CPAtlasPage input, IFormFileCollection files);
        /// <summary>Deletes a record.</summary>
        CPAtlasPage RemovePage(int id);
        /// <summary>Returns the dashboard of the app.</summary>
        CPAtlasDashboard LoadDashboard();
        /// <summary>Writes a client audit entry.</summary>
        void AuditLog(CPAtlasPage page);
        /// <summary>Returns the audit log of a record.</summary>
        List<CPAuditLog> GetAuditLog(string appCode, int refID);
    }

    /// <summary>The service of an app with a section form.</summary>
    internal interface ISectionService
    {
        /// <summary>Returns the form section of a record.</summary>
        CPApplicationSection LoadPage(CPAtlasPage page);
    }

    /// <summary>The service of an app with a step form.</summary>
    internal interface IStepService
    {
        /// <summary>Returns the configuration of a step.</summary>
        CPAtlasApplicationConfig LoadStep(CPAtlasPage page);
        /// <summary>Returns the step of a record.</summary>
        CPApplicationStep LoadPage(CPAtlasPage page);
    }

    /// <summary>The service of an app with named actions.</summary>
    public interface IActionService
    {
        /// <summary>Runs an action on a page and returns the section.</summary>
        CPApplicationSection Execute(string action, CPAtlasPage page);
    }

    /// <summary>The service of an app with attachments.</summary>
    internal interface IAttachmentService
    {
        /// <summary>The folder of the attachments.</summary>
        string? AttachmentFolderName { get; set; }
        /// <summary>Removes an attachment.</summary>
        void RemoveAttachment(int id);
    }
}
