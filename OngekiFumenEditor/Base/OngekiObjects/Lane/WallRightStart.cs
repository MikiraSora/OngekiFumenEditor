﻿using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Wall.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.Wall
{
    public class WallRightStart : WallStartBase
    {
        public override string IDShortName => "WRS";

        public override LaneType LaneType => LaneType.WallRight;
        public override Type NextType => typeof(WallRightNext);
        public override Type EndType => typeof(WallRightEnd);
    }
}
