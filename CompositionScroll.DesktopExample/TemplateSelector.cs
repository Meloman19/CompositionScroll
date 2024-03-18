using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using System.Collections.Generic;

namespace CompositionScroll.DesktopExample
{
    public sealed class TemplateSelector : IDataTemplate
    {
        [Content]
        public Dictionary<string, IDataTemplate> Templates { get; } = new Dictionary<string, IDataTemplate>();

        public Control Build(object param)
        {
            return Templates[param as string].Build(param);
        }

        public bool Match(object param)
        {
            if (param is not string key)
                return false;

            return Templates.ContainsKey(key);
        }
    }
}