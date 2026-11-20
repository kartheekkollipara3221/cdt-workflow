using BBH.CP.Atlas.Service.Platform.Attachments;
using BBH.CP.Atlas.Service.Platform.Registry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BBH.CP.Atlas.App.Platform;

/// <summary>
/// Gives the stored files of the platform apps for a download.
/// </summary>
/// <remarks>
/// The route takes the app code as text, so each platform app can use it. The current CDT routes v1/Download and
/// v1/RemoveAttachment do not change. A platform app removes a file in the save, so this controller has no remove route.
/// </remarks>
[Authorize]
[ApiController]
public sealed class AtlasAttachmentController(IAtlasModuleResolver resolver) : ControllerBase
{
    /// <summary>
    /// Returns one stored file as a download, after the View check of its record.
    /// </summary>
    /// <remarks>
    /// The response always has Content-Disposition attachment and X-Content-Type-Options nosniff. Thus the browser
    /// does not run an uploaded HTML or script file. The content type comes from the stored file name.
    /// </remarks>
    /// <response code="404">The app, the file or the record is not there, or a rule hides the file.</response>
    /// <response code="403">The user has no View access to the record.</response>
    [HttpGet]
    [Route("v1/Attachment/{appCode}/{id:int}")]
    public IActionResult Download(string appCode, int id)
    {
        AtlasAttachmentContent file = resolver.Get<IAtlasAttachmentSource>(appCode, "files").OpenAttachment(id);
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(file.Content, file.ContentType, file.FileName);
    }
}
