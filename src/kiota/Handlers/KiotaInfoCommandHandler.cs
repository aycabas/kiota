using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Rendering.Views;
using System.CommandLine.Rendering;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kiota.Builder;
using Kiota.Builder.Configuration;
using Microsoft.Extensions.Logging;

namespace kiota.Handlers;
internal class KiotaInfoCommandHandler : KiotaSearchBasedCommandHandler {
    public Option<string> DescriptionOption { get;set; }
    public Option<bool> ClearCacheOption { get; set; }
    public Option<string> SearchTermOption { get; set; }
    public Option<string> VersionOption { get; set; }
    public Option<GenerationLanguage?> GenerationLanguage { get; set; }
    public override async Task<int> InvokeAsync(InvocationContext context)
    {
        string openapi = context.ParseResult.GetValueForOption(DescriptionOption);
        bool clearCache = context.ParseResult.GetValueForOption(ClearCacheOption);
        string searchTerm = context.ParseResult.GetValueForOption(SearchTermOption);
        string version = context.ParseResult.GetValueForOption(VersionOption);
        GenerationLanguage? language = context.ParseResult.GetValueForOption(GenerationLanguage);
        CancellationToken cancellationToken = (CancellationToken)context.BindingContext.GetService(typeof(CancellationToken));
        var (loggerFactory, logger) = GetLoggerAndFactory<KiotaBuilder>(context);
        Configuration.Search.SearchTerm = searchTerm;
        Configuration.Search.Version = version;
        Configuration.Search.ClearCache = clearCache;
        using (loggerFactory) {

            if(!language.HasValue) {
                ShowLanguagesTable();
                DisplayInfoAdvanced();
                return 0;
            }

            var (searchResultDescription, statusCode) = await GetDescriptionFromSearch(openapi, searchTerm, loggerFactory, logger, cancellationToken);
            if (statusCode.HasValue) {
                return statusCode.Value;
            }
            if (!string.IsNullOrEmpty(searchResultDescription)) {
                openapi = searchResultDescription;
            }

            Configuration.Generation.OpenAPIFilePath = openapi;
            Configuration.Generation.ClearCache = clearCache;
            Configuration.Generation.Language = language.Value;

            var instructions = Configuration.Languages;
            if(!string.IsNullOrEmpty(openapi))
                try {
                    var builder = new KiotaBuilder(logger, Configuration.Generation);
                    var result = await builder.GetLanguageInformationAsync(cancellationToken);
                    if (result != null)
                        instructions = result;
                } catch (Exception ex) {
#if DEBUG
                    logger.LogCritical(ex, "error getting information from the description: {exceptionMessage}", ex.Message);
                    throw; // so debug tools go straight to the source of the exception when attached
#else
                    logger.LogCritical("error getting information from the description: {exceptionMessage}", ex.Message);
                    return 1;
#endif
                }
            ShowLanguageInformation(language.Value, instructions);
            return 0;
        }
    }
    private void ShowLanguagesTable() {
        var defaultInformation = Configuration.Languages;
        var view = new TableView<KeyValuePair<string, LanguageInformation>>() {
            Items = defaultInformation.OrderBy(static x => x.Key).Select(static x => x).ToList(),
        };
        view.AddColumn(static x => x.Key, "Language");
        view.AddColumn(static x => x.Value.MaturityLevel.ToString(), "Maturity Level");
        var console = new SystemConsole();
        using var terminal = new SystemConsoleTerminal(console);
        var layout = new StackLayoutView { view };
        console.Append(layout);
    }
    private static void ShowLanguageInformation(GenerationLanguage language, LanguagesInformation informationSource) {
        if (informationSource.TryGetValue(language.ToString(), out var languageInformation)) {
            DisplayInfo($"The language {language} is currently in {languageInformation.MaturityLevel} maturity level.",
                        "After generating code for this language, you need to install the following packages:");
            foreach(var dependency in languageInformation.Dependencies) {
                DisplayInfo(string.Format(languageInformation.DependencyInstallCommand, dependency.Name, dependency.Version));
            }
        } else {
            DisplayInfo($"No information for {language}.");
        }
    }
}
