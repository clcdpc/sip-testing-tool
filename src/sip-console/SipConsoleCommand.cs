using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Clc.Sip;

namespace sip_console
{
    public class SipConsoleCommand
    {
        public int? Sequence { get; set; }
        public string Name { get; set; }
        public string Method { get; set; }
        public string FixedFields { get; set; }
        public Dictionary<string, string> VariableFields { get; set; } = new Dictionary<string, string>();
        public string SuccessRegex { get; set; }
        public string FieldTerminator { get; set; } = "|";

        string CommandString => ToCommandString();

        Dictionary<string, string> ParameterNameMap = new Dictionary<string, string>
        {
            ["patronBarcode"] = "AA",
            ["itemBarcode"] = "AB",
            ["patronPin"] = "AD",
            ["itemProperties"] = "CH",
            ["feeAcknowledged"] = "BO",
            ["vendorProfile"] = "VP",
            ["locationCode"] = "CP",
            ["sipUsername"] = "CN",
            ["sipPassword"] = "CO",
            ["itemTitle"] = "AJ",
        };

        public string ToCommandString()
        {
            return ReplacePlaceholders($"{Method}{FixedFields}{JoinVariableFields()}{FieldTerminator}");
        }

        string ReplacePlaceholders(string input)
        {
            var output = input.Replace("{now}", DateTime.Now.ToSipString());
            return output;
        }

        string JoinVariableFields()
        {
            return string.Join(FieldTerminator, VariableFields.Select(p => $"{ParameterNameMap.SingleOrDefault(m => string.Equals(m.Key, p.Key, StringComparison.OrdinalIgnoreCase)).Value ?? p.Key}{p.Value}"));
        }
    }
}
