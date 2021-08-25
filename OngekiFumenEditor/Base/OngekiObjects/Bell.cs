using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class Bell : OngekiTimelineObjectBase, IHorizonPositionObject , IDisplayableObject
    {
        public XGrid XGrid { get; set; } = new XGrid();

        public static string CommandName => "BEL";
        public override string IDShortName => CommandName;

        public Type ModelViewType => typeof(BellViewModel);

        public override string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {TGrid.Serialize(fumenData)} {XGrid.Serialize(fumenData)}";
        }
    }
}
