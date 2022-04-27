﻿using OngekiFumenEditor.Base;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.DefaultImpl.NyagekiCommandParserImpl
{
    internal static class ParserUtils
    {
        public static IDisposable GetValuesMapWithDisposable(this string paramsDataStr, out Dictionary<string, string> map)
        {
            return ParseParams(paramsDataStr).ToDictionaryWithObjectPool(x => x.name, x => x.value, out map);
        }

        private static Regex s = new Regex(@"(\w+)\[(.*?)\]");

        public static IEnumerable<(string name, string value)> ParseParams(string content)
        {
            foreach (Match m in s.Matches(content))
                yield return (m.Groups[1].Value, m.Groups[2].Value);
        }

        public static TGrid ParseToTGrid(this string tgridContent)
        {
            var data = tgridContent.Split(",");
            return new TGrid(float.Parse(data[0]), int.Parse(data[1]));
        }

        public static XGrid ParseToXGrid(this string xgridContent)
        {
            var data = xgridContent.Split(",");
            return new XGrid(float.Parse(data[0]), int.Parse(data[1]));
        }
    }
}