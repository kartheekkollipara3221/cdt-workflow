// STAND-IN: do not copy to the target.
// Target: Service/CPApplicationService.cs. Same properties and the abstract and virtual members that the
// platform overrides or calls. The target bodies (MapData, BindData, workflow helpers) are not copied:
// the platform does not use them. The virtual bodies here throw, so a test shows any unexpected call.
using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Enumerations;
using BBH.CP.Atlas.Service.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace BBH.CP.Atlas.Service
{
    /// <summary>The local stand-in of the base class of the app services.</summary>
    public abstract class CPApplicationService : IApplicationService
    {
        #region Properties

        /// <inheritdoc/>
        public CPSharepointContext? Context { get; set; }
        /// <inheritdoc/>
        public IConfiguration? Configuration { get; set; }
        /// <inheritdoc/>
        public CPUser? User { get; set; }
        /// <inheritdoc/>
        public CPApplicationCode Code { get; set; }
        /// <inheritdoc/>
        public CPApplicationCode? ParentCode { get; set; }
        /// <inheritdoc/>
        public CPAtlasApplicationConfig? AppConfig { get; set; }
        /// <inheritdoc/>
        public CPAtlasDashboard? Dashboard { get; set; }

        #endregion Properties

        #region Abstract Method

        /// <inheritdoc/>
        public abstract CPAtlasPage SavePage(CPAtlasPage input, IFormFileCollection files);
        /// <inheritdoc/>
        public abstract CPAtlasPage RemovePage(int id);

        #endregion Abstract Method

        #region Virtual Method

        /// <summary>Throws in the stand-in. The platform overrides it.</summary>
        /// <exception cref="NotSupportedException">Always.</exception>
        public virtual CPAtlasDashboard LoadDashboard()
        {
            throw new NotSupportedException("Stand-in: the legacy LoadDashboard is not available locally.");
        }

        /// <summary>Throws in the stand-in. The platform overrides it.</summary>
        /// <exception cref="NotSupportedException">Always.</exception>
        public virtual void AuditLog(CPAtlasPage page)
        {
            throw new NotSupportedException("Stand-in: the legacy AuditLog is not available locally.");
        }

        /// <summary>Throws in the stand-in. The platform overrides it.</summary>
        /// <exception cref="NotSupportedException">Always.</exception>
        public virtual List<CPAuditLog> GetAuditLog(string appCode, int refID)
        {
            throw new NotSupportedException("Stand-in: the legacy GetAuditLog is not available locally.");
        }

        #endregion Virtual Method
    }
}
