using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PuppeteerSharp;

namespace PmesCSharp.Services;

public class PdfService
{
    private readonly IServiceProvider _provider;

    public PdfService(IServiceProvider provider)
    {
        _provider = provider;
    }

    public async Task<string> RenderViewToStringAsync(Controller controller, string viewName, object model)
    {
        var httpContext = controller.HttpContext;
        var actionContext = new ActionContext(httpContext, httpContext.GetRouteData(), controller.ControllerContext.ActionDescriptor);

        var viewEngine = _provider.GetRequiredService<ICompositeViewEngine>();
        var tempDataProvider = _provider.GetRequiredService<ITempDataProvider>();

        var viewResult = viewEngine.FindView(actionContext, viewName, false);
        if (!viewResult.Success)
            throw new InvalidOperationException($"View '{viewName}' not found.");

        using var sw = new StringWriter();
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model
        };
        var viewContext = new ViewContext(actionContext, viewResult.View, viewData, new TempDataDictionary(actionContext.HttpContext, tempDataProvider), sw, new HtmlHelperOptions());
        await viewResult.View.RenderAsync(viewContext);
        return sw.ToString();
    }

    public async Task<byte[]> GeneratePdfFromHtmlAsync(string html)
    {
        // Ensure Chromium downloaded
        await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultChromiumRevision);
        using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
        using var page = await browser.NewPageAsync();
        await page.SetContentAsync(html);
        var bytes = await page.PdfDataAsync(new PdfOptions { Format = PuppeteerSharp.Media.PaperFormat.A4, PrintBackground = true, MarginOptions = new PuppeteerSharp.Media.MarginOptions { Top = "1in", Bottom = "1in", Left = "0.5in", Right = "0.5in" } });
        return bytes;
    }
}
