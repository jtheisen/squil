using Microsoft.AspNetCore.Components;

namespace Squil
{
    [EventHandler("onhiddenbsmodal", typeof(HiddenBsModalEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
    public static class EventHandlers
    {
    }

    public class HiddenBsModalEventArgs : EventArgs
    {

    }
}
