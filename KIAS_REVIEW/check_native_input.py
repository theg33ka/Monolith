import re
from pathlib import Path

root=Path(__file__).resolve().parent.parent
source=(root/'Tools/KiasBenchmark/Server/NativeLab.cs').read_text(encoding='utf8')
record=re.search(r'    private sealed record Input\([^\n]+\);',source).group()
body=re.search(r'_inputs = File.ReadLines\(scenario\).Select\(line =>\s*\{(.*?)\n\s*\}\).OrderBy',source,re.S).group(1)
directory=root/'KIAS_REVIEW/native-input-check'
directory.mkdir(exist_ok=True)
(directory/'NativeInputCheck.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>',encoding='utf8')
program='''using System.Text.Json;
internal static class Program
{
RECORD
    private static Input Parse(string line)
    {
BODY
    }
    private static string Line(bool include, object? rotation)
    {
        var row = new Dictionary<string, object?> { ["id"] = "rotation", ["tick"] = 120, ["ship"] = "ship-0001", ["type"] = "collision.start" };
        if (include) row["fixtureRotationDegrees"] = rotation;
        return JsonSerializer.Serialize(row);
    }
    public static void Main()
    {
        if (Parse(Line(false, null)).FixtureRotationDegrees != null) throw new Exception("Absent angle must remain absent.");
        foreach (var angle in new[] { 0d, 37d, -.054d, 359.9d })
        {
            var actual = Parse(Line(true, angle)).FixtureRotationDegrees;
            if (actual is not { } value || Math.Abs(value - angle) > .001) throw new Exception("Lost requested angle.");
        }
        foreach (var invalid in new object?[] { "bad", 1e100, null })
        {
            var rejected = false;
            try { Parse(Line(true, invalid)); }
            catch (InvalidOperationException) { rejected = true; }
            if (!rejected) throw new Exception("Invalid angle accepted.");
        }
        Console.WriteLine("PASS: actual NativeLab JSON loader; absent, zero, 37, -0.054, 359.9 and three invalid inputs.");
    }
}
'''
(directory/'Program.cs').write_text(program.replace('RECORD',record).replace('BODY',body),encoding='utf8')
print('Extracted the actual Input declaration and JSON loader body; no hand-written parser substitute.')
