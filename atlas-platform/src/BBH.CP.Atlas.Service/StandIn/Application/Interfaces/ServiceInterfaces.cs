// STAND-IN: do not copy to the target.
// Target: Service/Application/Interfaces/*.cs. Same names, same access modifiers, same members.
// IWorkflowService is left out (workflow engine types; no workflow in P1).
using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Enumerations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace BBH.CP.Atlas.Service.Application.Interfaces
{
    public interface IApplicationService
    {
        CPSharepointContext? Context { get; set; }
        IConfiguration? Configuration { get; set; }
        CPUser? User { get; set; }
        CPApplicationCode Code { get; set; }
        CPApplicationCode? ParentCode { get; set; }
        CPAtlasApplicationConfig? AppConfig { get; set; }
        CPAtlasDashboard? Dashboard { get; set; }
        CPAtlasPage SavePage(CPAtlasPage input, IFormFileCollection files);
        CPAtlasPage RemovePage(int id);
        CPAtlasDashboard LoadDashboard();
        void AuditLog(CPAtlasPage page);
        List<CPAuditLog> GetAuditLog(string appCode, int refID);
    }

    internal interface ISectionService
    {
        CPApplicationSection LoadPage(CPAtlasPage page);
    }

    internal interface IStepService
    {
        CPAtlasApplicationConfig LoadStep(CPAtlasPage page);
        CPApplicationStep LoadPage(CPAtlasPage page);
    }

    public interface IActionService
    {
        CPApplicationSection Execute(string action, CPAtlasPage page);
    }

    internal interface IAttachmentService
    {
        string? AttachmentFolderName { get; set; }
        void RemoveAttachment(int id);
    }
}
