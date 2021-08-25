﻿using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.CommandParserImpl.MetaInfo
{
    [Export(typeof(ICommandParser))]
    class CreatorCommandParser : MetaInfoCommandParserBase
    {
        public override string CommandLineHeader => "CREATOR";

        public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
        {
            fumen.MetaInfo.Creator = args.GetData<string>(1) ?? "";
        }
    }
}
