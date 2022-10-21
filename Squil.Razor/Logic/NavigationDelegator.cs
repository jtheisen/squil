using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Squil;

public class NavigationDelegator
{
    private readonly NavigationManager navigationManager;

    public NavigationDelegator(NavigationManager navigationManager)
    {
        this.navigationManager = navigationManager;
    }

    [JSInvokable]
    public void Navigate(String url, Boolean replace)
    {
        navigationManager.NavigateTo(url, replace: replace);
    }
}
