using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;
using PulseOffice.Sheets.Engine.Evaluation.Functions.SpecialForms;
using PulseOffice.Sheets.Engine.Recalculation;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public static class SheetsEngineServiceCollectionExtensions
{
    public static IServiceCollection AddSheetsEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<Localization.IFormulaPunctuationConvention, Localization.FormulaPunctuationConvention>();
        services.TryAddSingleton<Localization.IFunctionNameVocabulary, Localization.FunctionNameVocabulary>();
        services.TryAddSingleton<Parsing.IFormulaExpressionParser, Parsing.FormulaParser>();
        services.TryAddSingleton<Localization.IFormulaTextTranslator, Localization.FormulaTextTranslator>();
        services.TryAddSingleton<Localization.IFormulaLocalization, Localization.FormulaLocalization>();
        services.AddFunctionProvider<ArithmeticFunctions>();
        services.AddFunctionProvider<TrigonometryFunctions>();
        services.AddFunctionProvider<RoundingFunctions>();
        services.AddFunctionProvider<StatisticsFunctions>();
        services.AddFunctionProvider<LogicalFunctions>();
        services.AddFunctionProvider<TextFunctions>();
        services.AddFunctionProvider<InformationFunctions>();
        services.AddFunctionProvider<NumberSystemFunctions>();
        services.AddFunctionProvider<CombinatoricsFunctions>();
        services.AddFunctionProvider<RandomFunctions>();
        services.AddFunctionProvider<DateTimeFunctions>();
        services.AddFunctionProvider<LookupFunctions>();
        services.AddFunctionProvider<ConditionalFunctions>();
        services.AddFunctionProvider<DistributionFunctions>();
        services.AddFunctionProvider<FinancialFunctions>();
        services.AddFunctionProvider<EngineeringFunctions>();
        services.AddFunctionProvider<RegressionFunctions>();
        services.AddFunctionProvider<IntegrationFunctions>();
        services.AddFunctionProvider<InverseDistributionFunctions>();
        services.AddFunctionProvider<DatabaseFunctions>();
        services.AddFunctionProvider<TextualAggregateFunctions>();
        services.AddFunctionProvider<MiscellaneousFunctions>();
        services.AddFunctionProvider<SecuritiesFunctions>();
        services.AddFunctionProvider<ReferenceFunctions>();
        services.AddFunctionProvider<ArrayFunctions>();
        services.AddFunctionProvider<ModernLookupFunctions>();
        services.AddFunctionProvider<SummaryFunctions>();
        services.AddFunctionProvider<CompatibilityFunctions>();
        services.AddFunctionProvider<ExtendedFunctions>();
        services.AddFunctionProvider<RegularExpressionFunctions>();
        services.AddFunctionProvider<ForecastFunctions>();
        services.AddFunctionProvider<ExoticFinanceFunctions>();

        services.TryAddSingleton<IRandomSource, SystemRandomSource>();
        services.TryAddSingleton<IGuardedRegex, GuardedRegex>();
        services.TryAddSingleton<IDateSystem, StandardDateSystem>();
        services.TryAddSingleton<IEtsForecaster, EtsForecaster>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<PulseOffice.Platform.IHostSystem, PulseOffice.Platform.RuntimeHostSystem>();
        services.TryAddSingleton<ISystemClock, SystemClock>();
        services.TryAddSingleton<PulseOffice.Sheets.Formatting.ISpreadsheetLocale, PulseOffice.Sheets.Formatting.SystemSpreadsheetLocale>();
        services.TryAddSingleton<PulseOffice.Sheets.Formatting.ISystemDateCode, PulseOffice.Sheets.Formatting.SystemDateCode>();
        services.TryAddSingleton<NumericText>();
        services.TryAddSingleton<ILocalizedFormatTokens, LocalizedFormatTokens>();
        services.TryAddSingleton<ReferenceText>();
        services.TryAddSingleton<ArrayParameterClasses>();
        services.TryAddSingleton<HolidayCalendar>();
        services.TryAddSingleton<IWorkdayArithmetic, WorkdayArithmetic>();
        services.TryAddSingleton<WorkdayCalculator>();
        services.TryAddSingleton<PulseOffice.Sheets.Formatting.INumberFormatParser, PulseOffice.Sheets.Formatting.NumberFormatParser>();
        services.TryAddSingleton<IFormatCodeRenderer, FormatCodeRenderer>();
        services.TryAddSingleton<IRangeMaterializer, RangeMaterializer>();
        services.AddSpecialForm<LambdaForm>();
        services.AddSpecialForm<LetForm>();
        services.AddSpecialForm<CurrentForm>();
        services.AddSpecialForm<IsOmittedForm>();
        services.AddSpecialForm<MapForm>();
        services.AddSpecialForm<ReduceForm>();
        services.AddSpecialForm<ScanForm>();
        services.AddSpecialForm<ByRowForm>();
        services.AddSpecialForm<ByColForm>();
        services.AddSpecialForm<MakeArrayForm>();

        services.TryAddSingleton<IFunctionRegistry, FunctionRegistry>();
        services.TryAddSingleton<ISpecialFormRegistry, SpecialFormRegistry>();
        services.TryAddSingleton<INameExpansion, RelativeNameExpansion>();
        services.TryAddSingleton<INameAnchoring, ActiveCellNameAnchoring>();
        services.TryAddSingleton<Evaluator>();
        services.TryAddSingleton<PulseOffice.Sheets.Formatting.IFormatKindClassifier, PulseOffice.Sheets.Formatting.FormatKindClassifier>();
        services.TryAddSingleton<PulseOffice.Sheets.Formatting.IFormatHints, PulseOffice.Sheets.Formatting.FormatHints>();
        services.TryAddSingleton<Formats.IFunctionFormatRules, Formats.FunctionFormatRules>();
        services.TryAddSingleton<Formats.IFormulaFormatInference, Formats.FormulaFormatInference>();
        services.TryAddSingleton<PulseOffice.Foundation.Identity.IUserIdentity, PulseOffice.Foundation.Identity.SystemUserIdentity>();
        services.TryAddSingleton<ICalculationEngineFactory, CalculationEngineFactory>();
        return services;
    }

    public static IServiceCollection AddFunctionProvider<TProvider>(this IServiceCollection services)
    where TProvider : class, IFunctionProvider
    {
        services.AddSingleton<IFunctionProvider, TProvider>();
        return services;
    }

    public static IServiceCollection AddSpecialForm<TForm>(this IServiceCollection services)
    where TForm : class, ISpecialForm
    {
        services.AddSingleton<ISpecialForm, TForm>();
        return services;
    }
}
