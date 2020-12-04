using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEngine.Experimental.UIElements.StyleEnums;
using UnityEditor;
using UnityEditor.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;

namespace MomomaAssets
{

    static class UIElementsUtility
    {
        internal static VisualElement CreateLabeledElement(string lablel, VisualElement element)
        {
            var horizontalElement = new VisualElement() { style = { flexDirection = FlexDirection.Row } };
            element.style.flexGrow = 1f;
            horizontalElement.Add(new Label(lablel));
            horizontalElement.Add(element);
            return horizontalElement;
        }
    }

}
