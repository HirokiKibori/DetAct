using DetAct.Data.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DetAct.Pages
{
    public class DownloadModel : PageModel
    {
        private DirectoryWatcherService _directoryWatcherService;

        public DownloadModel(DirectoryWatcherService directoryWatcherService) => _directoryWatcherService = directoryWatcherService;

        public IActionResult OnGet(string name)
        {
            var file = _directoryWatcherService.GetFile(name);

            if(file is null)
                return Redirect("/treeeditor");

            return File(file, "application/octet-stream", name + ".btml");
        }
    }
}
