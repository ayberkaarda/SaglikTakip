using Microsoft.Extensions.DependencyInjection;

namespace SaglikTakip.UI.Infrastructure;

/// <summary>Creates forms with their dependencies resolved from the container.</summary>
public interface IFormFactory
{
    T Create<T>()
        where T : Form;
}

internal sealed class FormFactory(IServiceProvider serviceProvider) : IFormFactory
{
    // ActivatorUtilities is used instead of registering forms as transients so the container
    // does not keep every opened form alive until the application exits.
    public T Create<T>()
        where T : Form => ActivatorUtilities.CreateInstance<T>(serviceProvider);
}
