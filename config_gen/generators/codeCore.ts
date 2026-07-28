import { ConfigEntry, ConfigEntryType } from "../types"
import { trimEnd } from "../utils";
import { getConfigDefaultValueText } from "../utils";

const INDENT = "    ";

function indent(text: string, level: number): string {
    const prefix = INDENT.repeat(level);
    const hasTrailing = text.endsWith('\n');
    const body = hasTrailing ? text.slice(0, -1) : text;
    return body.split('\n').map(line => prefix + line).join('\n') + (hasTrailing ? '\n' : '');
}

export default function (data: ConfigEntry[]): string {
    let result = `using System.ComponentModel;
using HierarchicalPropertyDefault;
using Newtonsoft.Json;

#nullable enable
namespace BililiveRecorder.Core.Config.V3
{
`;

    function write_property(r: ConfigEntry) {
        let block = `/// <summary>\n/// ${r.name}\n/// </summary>\n`;
        block += `public ${r.type} ${r.id} { get => this.GetPropertyValue<${trimEnd(r.type, '?')}>(); set => this.SetPropertyValue(value); }\n`;
        block += `public bool Has${r.id} { get => this.GetPropertyHasValue(nameof(this.${r.id})); set => this.SetPropertyHasValue<${trimEnd(r.type, '?')}>(value, nameof(this.${r.id})); }\n`;
        block += `[JsonProperty(nameof(${r.id})), EditorBrowsable(EditorBrowsableState.Never)]\n`;
        block += `public Optional<${r.type}> Optional${r.id} { get => this.GetPropertyValueOptional<${trimEnd(r.type, '?')}>(nameof(this.${r.id})); set => this.SetPropertyValueOptional(value, nameof(this.${r.id})); }\n`;
        result += indent(block, 2) + '\n';
    }

    function write_readonly_property(r: ConfigEntry) {
        let block = `/// <summary>\n/// ${r.name}\n/// </summary>\n`;
        block += `public ${r.type} ${r.id} => this.GetPropertyValue<${trimEnd(r.type, '?')}>();\n`;
        result += indent(block, 2) + '\n';
    }

    {
        result += indent("[JsonObject(MemberSerialization.OptIn)]\n", 1);
        result += indent("public sealed partial class RoomConfig : HierarchicalObject<GlobalConfig, RoomConfig>\n", 1);
        result += indent("{\n", 1);

        data.filter(x => x.configType != 'globalOnly').forEach(r => write_property(r));
        data.filter(x => x.configType == 'globalOnly').forEach(r => write_readonly_property(r));

        result += indent("}\n", 1) + '\n';
    }

    {
        result += indent("[JsonObject(MemberSerialization.OptIn)]\n", 1);
        result += indent("public sealed partial class GlobalConfig : HierarchicalObject<DefaultConfig, GlobalConfig>\n", 1);
        result += indent("{\n", 1);

        data.filter(x => x.configType != 'roomOnly').forEach(r => write_property(r));

        result += indent("}\n", 1) + '\n';
    }

    {
        result += indent(`public sealed partial class DefaultConfig
`, 1);
        result += indent(`{
`, 1);
        result += indent(`public static readonly DefaultConfig Instance = new DefaultConfig();
`, 2);
        result += indent(`private DefaultConfig() {}\n`, 2) + '\n';

        data
            .filter(x => x.configType != 'roomOnly')
            .forEach(r => {
                result += indent(`public ${trimEnd(r.type, '?')} ${r.id} => ${getConfigDefaultValueText(r)};\n`, 2) + '\n';
            });

        result += indent("}\n", 1) + '\n';
    }

    result += `}\n`;
    return result;
}
