using Stride.Engine;
using Stride.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace StrideEdExt.UI;

public readonly struct UIElementKey<TUIElement> : IEquatable<UIElementKey<TUIElement>>
    where TUIElement : UIElement
{
    public readonly string UIName;

    public readonly Type ControlType => typeof(TUIElement);

    public UIElementKey(string uiName)
    {
        UIName = uiName;
    }

    public static bool operator ==(UIElementKey<TUIElement> left, UIElementKey<TUIElement> right) => left.Equals(right);

    public static bool operator !=(UIElementKey<TUIElement> left, UIElementKey<TUIElement> right) => !left.Equals(right);

    public readonly override bool Equals(object? obj) => (obj is UIElementKey<TUIElement> val) && Equals(val);

    public readonly bool Equals(UIElementKey<TUIElement> other)
    {
        return UIName == other.UIName;
    }

    public readonly override int GetHashCode() => UIName.GetHashCode();
}

public static class UIElementKeyExtensions
{
    public static bool TryGetUI<TUIElement>(this UIElement searchUIElement, UIElementKey<TUIElement> key, [NotNullWhen(true)] out TUIElement? uiElement)
        where TUIElement : UIElement
    {
        uiElement = (TUIElement)searchUIElement.FindName(key.UIName);
        return uiElement is not null;
    }

    public static bool TryGetUI<TUIElement>(this UIComponent uiComponent, UIElementKey<TUIElement> key, [NotNullWhen(true)] out TUIElement? uiElement)
        where TUIElement : UIElement
    {
        Debug.Assert(uiComponent is not null, $"UIComponent not assigned.");
        return TryGetUI(uiComponent.Page, key, out uiElement);
    }

    public static bool TryGetUI<TUIElement>(this UIPage uiPage, UIElementKey<TUIElement> key, [NotNullWhen(true)] out TUIElement? uiElement)
        where TUIElement : UIElement
    {
        uiElement = (TUIElement)uiPage.RootElement.FindName(key.UIName);
        return uiElement is not null;
    }

    public static TUIElement GetUI<TUIElement>(this UIElement uiElement, UIElementKey<TUIElement> key)
        where TUIElement : UIElement
    {
        Debug.Assert(uiElement is not null, $"UIElement not assigned.");
        var element = (TUIElement)uiElement.FindName(key.UIName);
        Debug.Assert(element is not null, $"UIElement {key.UIName} not found.");
        return element;
    }

    public static TUIElement GetUI<TUIElement>(this UIComponent uiComponent, UIElementKey<TUIElement> key)
        where TUIElement : UIElement
    {
        Debug.Assert(uiComponent is not null, $"UIComponent not assigned.");
        return GetUI(uiComponent!.Page, key);
    }

    public static TUIElement GetUI<TUIElement>(this UIPage uiPage, UIElementKey<TUIElement> key)
        where TUIElement : UIElement
    {
        var element = (TUIElement)uiPage.RootElement.FindName(key.UIName);
        Debug.Assert(element is not null, $"UIElement {key.UIName} not found.");
        return element!;
    }

    public static TUIElement GetChildUI<TUIElement>(this UIElement uiElement, UIElementKey<TUIElement> key)
        where TUIElement : UIElement
    {
        var element = uiElement.FindVisualChildOfType<TUIElement>(key.UIName);
        Debug.Assert(element is not null, $"UIElement {key.UIName} not found.");
        return element!;
    }

    public static IEnumerable<TUIElement> GetAllUI<TUIElement>(this UIComponent uiComponent, UIElementKey<TUIElement> key)
        where TUIElement : UIElement
    {
        Debug.Assert(uiComponent is not null, $"UIComponent not assigned.");
        return GetAllUI(uiComponent!.Page, key);
    }

    public static IEnumerable<TUIElement> GetAllUI<TUIElement>(this UIPage uiPage, UIElementKey<TUIElement> key)
        where TUIElement : UIElement
    {
        var rootElement = uiPage.RootElement;
        return GetAllUI<TUIElement>(rootElement, key.UIName);
    }

    public static IEnumerable<TUIElement> GetAllUI<TUIElement>(this UIElement element, UIElementKey<TUIElement> key)
        where TUIElement : UIElement
    {
        return GetAllUI<TUIElement>(element, key.UIName);
    }

    private static IEnumerable<TUIElement> GetAllUI<TUIElement>(UIElement element, string uiName)
        where TUIElement : UIElement
    {
        if (element.Name == uiName)
        {
            yield return (TUIElement)element;
        }
        foreach (var ch in element.VisualChildren)
        {
            var foundUIChildren = GetAllUI<TUIElement>(ch, uiName);
            foreach (var uiChild in foundUIChildren)
            {
                yield return uiChild;
            }
        }
    }

    public static TUIElement Create<TUIElement>(this UILibrary library, UIElementKey<TUIElement> key)
        where TUIElement : UIElement
    {
        var uiElement = library.InstantiateElement<TUIElement>(key.UIName);
        return uiElement;
    }
}
