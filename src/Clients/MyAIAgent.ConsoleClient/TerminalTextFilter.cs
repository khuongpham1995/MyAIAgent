/**
 * MyAIAgent | Console Client
 *
 * Removes untrusted terminal control sequences while retaining state across streamed text chunks.
 */

using System.Globalization;
using System.Text;

namespace MyAIAgent.ConsoleClient;

internal sealed class TerminalTextFilter
{
    private int state;

    public void Reset() => state = 0;

    public string Filter(string text)
    {
        var result = new StringBuilder();
        foreach (var character in text)
        {
            switch (state)
            {
                case 1: // ESC followed by a CSI, a control string, or one ordinary escape command.
                    state = character switch
                    {
                        '[' => 2,
                        ']' or 'P' or 'X' or '^' or '_' => 3,
                        _ => 0
                    };
                    break;
                case 2: // CSI ends at its final byte.
                    state = character switch
                    {
                        >= '@' and <= '~' => 0,
                        '\u001b' => 1,
                        _ => state
                    };
                    break;
                case 3: // OSC/DCS/APC/PM/SOS strings end at BEL or ST.
                    state = character switch
                    {
                        '\a' or '\u009c' => 0,
                        '\u001b' => 4,
                        _ => state
                    };
                    break;
                case 4:
                    state = character == '\\' ? 0 : character == '\u001b' ? 4 : 3;
                    break;
                default:
                    switch (character)
                    {
                        case '\u001b':
                            state = 1;
                            break;
                        case '\u009b':
                            state = 2;
                            break;
                        case '\u009d' or '\u0090' or '\u0098' or '\u009e' or '\u009f':
                            state = 3;
                            break;
                        default:
                            {
                                if (character is '\n' or '\t'
                                    || !char.IsControl(character) && char.GetUnicodeCategory(character) != UnicodeCategory.Format)
                                    result.Append(character);
                                break;
                            }
                    }
                    break;
            }
        }
        return result.ToString();
    }
}
