using System.Collections.Generic;
using Rubberduck.VBA.Grammar;

namespace Rubberduck.VBA.ParseTreeListeners
{
    /// <summary>
    /// This class is not used, because the grammar (/generated parser)
    /// requires options to be specified first, or module options end up in an error node.
    /// </summary>
    public class ModuleOptionsListener : IVBBaseListener, IExtensionListener<VBParser.ModuleOptionContext>
    {
        private readonly IList<VBParser.ModuleOptionContext> _members = new List<VBParser.ModuleOptionContext>();
        public IEnumerable<VBParser.ModuleOptionContext> Members { get { return _members; } }

        public override void EnterModuleOptions(VBParser.ModuleOptionsContext context)
        {
            foreach (var option in context.moduleOption())
            {
                _members.Add(option);
            }
        }
    }
}