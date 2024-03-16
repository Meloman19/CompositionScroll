using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using System;
using System.Collections.Generic;

namespace CompositionScroll.DesktopExample
{
    public sealed class TemplateSelector : IDataTemplate
    {
        [Content]
        public Dictionary<Type, IDataTemplate> Templates { get; } = new Dictionary<Type, IDataTemplate>();

        public Control Build(object param)
        {
            var data = (param as ListViewModel).Data;
            return Templates[data.GetType()].Build(data);
        }

        public bool Match(object param)
        {
            var data = (param as ListViewModel)?.Data;
            if (data == null)
                return false;

            if (Templates.ContainsKey(data.GetType()))
                return true;

            return false;
        }
    }
}