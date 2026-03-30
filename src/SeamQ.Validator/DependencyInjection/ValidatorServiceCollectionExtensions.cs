using Microsoft.Extensions.DependencyInjection;
using SeamQ.Core.Abstractions;
using SeamQ.Validator.Rules;

namespace SeamQ.Validator.DependencyInjection;

public static class ValidatorServiceCollectionExtensions
{
    /// <summary>
    /// Registers the contract validator and all built-in validation rules.
    /// </summary>
    public static IServiceCollection AddSeamQValidator(this IServiceCollection services)
    {
        services.AddSingleton<IValidationRule, InterfaceImplementationRule>();
        services.AddSingleton<IValidationRule, InjectionTokenRule>();
        services.AddSingleton<IValidationRule, InputOutputRule>();
        services.AddSingleton<IContractValidator, ContractValidator>();
        return services;
    }
}
