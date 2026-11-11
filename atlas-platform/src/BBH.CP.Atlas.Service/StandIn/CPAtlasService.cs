// STAND-IN: do not copy to the target.
// Target: Service/CPAtlasService.cs. The target keeps its public methods and its if chains for the current codes.
// Integration: add the optional constructor parameters "resolver", "configStore" and "logger" at the end,
// so the current calls compile. Add the marked "PLATFORM" lines at the start of each method.
// The workflow engine parameters of the target constructor are left out here (release P1 has no workflow).
// At the target, change the "code" parameter of LoadDashboard and RemovePage from CPApplicationCode to string.
// After each PLATFORM line, the current if chain gets its enum with AtlasAppCode.ToLegacy(code, configStore):
//     LoadDashboard:   return LoadApplicationService(AtlasAppCode.ToLegacy(code, configStore))!.LoadDashboard();
//     LoadSectionPage: return LoadSectionService(page.Code ?? AtlasAppCode.ToLegacy(page.AppCode, configStore), workflowContextReader)!.LoadPage(page);
//     ManagePage:      return LoadApplicationService(page.Code ?? AtlasAppCode.ToLegacy(page.AppCode, configStore))!.SavePage(page, files);
//     RemovePage:      return LoadApplicationService(AtlasAppCode.ToLegacy(code, configStore))!.RemovePage(id);
//     AuditLog:        LoadApplicationService(page.Code ?? AtlasAppCode.ToLegacy(page.AppCode, configStore))!.AuditLog(page);
//     GetAuditLog:     return LoadApplicationService(AtlasAppCode.ToLegacy(appCode, configStore))!.GetAuditLog(appCode, refID);
// ToLegacy gives 404 for a platform app that is not available (Enabled = false), not the 500 of Enum.Parse.
// The target package holds this file as reference/StandIn/CPAtlasService.cs. Read it for the edits; do not copy it.
using System.Data;
using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Application.Interfaces;
using BBH.CP.Atlas.Service.Platform;
using BBH.CP.Atlas.Service.Platform.Registry;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BBH.CP.Atlas.Service
{
    /// <summary>
    /// The local stand-in of the Atlas service. Each method sends a platform app to its module; other codes give 404.
    /// </summary>
    public class CPAtlasService(CPSharepointContext context, CPPBContext? pbContext, IConfiguration configuration, CPUser user,
        IAtlasModuleResolver? resolver = null, IAtlasConfigStore? configStore = null, ILogger? logger = null)
    {
        /// <summary>Returns the app rows of the user from dbo.Get_Application, with the platform menu data.</summary>
        public List<Dictionary<string, object>> GetApplication()
        {
            DataTable dt = new();
            SqlParameter[] parameters = [new SqlParameter("@LanID", SqlDbType.VarChar, 100) { Value = user.LanID }];
            dt.Load(context.ExecuteDataReader("dbo.Get_Application", parameters));
            List<Dictionary<string, object>> result = dt.ToDictionaryList();

            // PLATFORM: add FormType and HasDashboard, and hide the apps that are not registered.
            // An error in this step returns the rows unchanged.
            if (resolver != null && configStore != null)
                result = AtlasApplicationMenu.Enrich(result, resolver, configStore, logger);

            return result;
        }

        /// <summary>Returns the dashboard of an app.</summary>
        public CPAtlasDashboard LoadDashboard(string code)
        {
            // PLATFORM
            if (Platform(code) is { } app)
                return resolver!.Get<IApplicationService>(app, "dashboard").LoadDashboard();

            throw Legacy(code);
        }

        /// <summary>Returns the form section of a record.</summary>
        public CPApplicationSection LoadSectionPage(CPAtlasPage page)
        {
            // PLATFORM
            if (Platform(page.AppCode ?? page.Code?.ToString()) is { } app)
                return resolver!.Get<ISectionService>(app, "section form").LoadPage(page);

            throw Legacy(page.AppCode ?? page.Code?.ToString());
        }

        /// <summary>Saves a record. <paramref name="input"/> is the page as JSON.</summary>
        public CPAtlasPage ManagePage(IFormFileCollection files, string input)
        {
            CPAtlasPage page = AtlasJson.Deserialize<CPAtlasPage>(input, "input");

            // PLATFORM
            if (Platform(page.AppCode ?? page.Code?.ToString()) is { } app)
                return resolver!.Get<IApplicationService>(app, "save").SavePage(page, files);

            throw Legacy(page.AppCode ?? page.Code?.ToString());
        }

        /// <summary>Deletes a record.</summary>
        public CPAtlasPage RemovePage(string code, int id)
        {
            // PLATFORM
            if (Platform(code) is { } app)
                return resolver!.Get<IApplicationService>(app, "delete").RemovePage(id);

            throw Legacy(code);
        }

        /// <summary>Writes a client audit entry. <paramref name="input"/> is the page as JSON.</summary>
        public void AuditLog(string input)
        {
            CPAtlasPage page = AtlasJson.Deserialize<CPAtlasPage>(input, "input");

            // PLATFORM: with AuditMode Server, the call does nothing.
            if (Platform(page.AppCode ?? page.Code?.ToString()) is { } app)
            {
                resolver!.Get<IApplicationService>(app, "audit").AuditLog(page);
                return;
            }

            throw Legacy(page.AppCode ?? page.Code?.ToString());
        }

        /// <summary>Returns the audit log of a record.</summary>
        public List<CPAuditLog> GetAuditLog(string appCode, int refID)
        {
            // PLATFORM
            if (Platform(appCode) is { } app)
                return resolver!.Get<IApplicationService>(app, "audit log").GetAuditLog(app, refID);

            throw Legacy(appCode);
        }

        /// <summary>Returns the normalized code of an enabled platform app, or null. With null, the current if chains run.</summary>
        private string? Platform(string? code)
        {
            if (resolver == null || string.IsNullOrWhiteSpace(code))
                return null;
            string text = AtlasAppCode.FromRequest(code);
            return resolver.IsPlatformApp(text) ? AtlasAppCode.Normalize(text) : null;
        }

        // Locally there are no current (legacy) services. At the target the current if chains run here, with the enum
        // from AtlasAppCode.ToLegacy. It gives 404 for a disabled platform app and 400 for no code.
        private AtlasProblemException Legacy(string? code)
        {
            try
            {
                AtlasAppCode.ToLegacy(code, configStore);
            }
            catch (ArgumentException)
            {
                // An unknown code: the target gives the error of Enum.Parse, and the stand-in gives 404.
            }
            return AtlasProblemException.NotFound($"App '{code}' is not available in the local stand-in host.");
        }

        private CPPBContext? PBContext => pbContext;

        private IConfiguration Configuration => configuration;
    }
}
