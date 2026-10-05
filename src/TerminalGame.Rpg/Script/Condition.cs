using System.Globalization;
using TerminalGame.Rpg.State;

namespace TerminalGame.Rpg.Script;

/// <summary>
/// A parsed boolean expression over the game state, used by scripts, exits and spots in content JSON.
/// <code>
/// expr       := and ( '||' and )*
/// and        := unary ( '&amp;&amp;' unary )*
/// unary      := '!' unary | '(' expr ')' | comparison
/// comparison := value ( ('==' | '!=' | '&gt;=' | '&lt;=' | '&gt;' | '&lt;') value )?    // a bare value means "≠ 0"
/// value      := integer | 'gold' | 'flag:' name | 'item:' id | 'party:' id | 'level:' id
/// </code>
/// <c>item:</c> counts the inventory; <c>party:</c> is 1 if that character has joined; <c>level:</c> is their level (0 if absent).
/// Examples: <c>flag:metRin == 0 &amp;&amp; party:hero</c>, <c>gold &gt;= 50 || item:forestKey</c>.
/// </summary>
public sealed class Condition
{
    private abstract record Node;

    private sealed record Value(string kind, string name, int literal) : Node;

    private sealed record Compare(Value left, string op, Value right) : Node;

    private sealed record Not(Node inner) : Node;

    private sealed record Binary(Node left, bool isAnd, Node right) : Node;

    private static readonly string[] valuePrefixes = { "flag", "item", "party", "level" };
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Condition> cache = new();

    private readonly Node root;

    private Condition(string source, Node root)
    {
        this.source = source;
        this.root = root;
    }

    public string source { get; }

    /// <summary>Every (kind, name) reference in the expression, e.g. ("item", "potion"), for content validation.</summary>
    public IEnumerable<(string kind, string name)> references => collect(root);

    /// <summary>Parses <paramref name="source"/>; throws <see cref="FormatException"/> with the position of the problem.</summary>
    public static Condition parse(string source)
    {
        Parser parser = new(source);
        Node node = parser.parseExpression();
        parser.expectEnd();
        return new Condition(source, node);
    }

    /// <summary>Null or blank conditions are always true. Parsed expressions are cached by source text.</summary>
    public static bool check(string? source, GameSession session) =>
        string.IsNullOrWhiteSpace(source) || cache.GetOrAdd(source, parse).evaluate(session);

    public bool evaluate(GameSession session) => eval(root, session);

    public override string ToString() => source;

    private static bool eval(Node node, GameSession session) => node switch
    {
        Binary { isAnd: true } b => eval(b.left, session) && eval(b.right, session),
        Binary b => eval(b.left, session) || eval(b.right, session),
        Not n => !eval(n.inner, session),
        Compare c => compare(valueOf(c.left, session), c.op, valueOf(c.right, session)),
        Value v => valueOf(v, session) != 0,
        _ => throw new InvalidOperationException(),
    };

    private static bool compare(int left, string op, int right) => op switch
    {
        "==" => left == right,
        "!=" => left != right,
        ">=" => left >= right,
        "<=" => left <= right,
        ">" => left > right,
        "<" => left < right,
        _ => throw new InvalidOperationException(op),
    };

    private static int valueOf(Value v, GameSession session) => v.kind switch
    {
        "number" => v.literal,
        "gold" => session.gold,
        "flag" => session.flags[v.name],
        "item" => session.inventory.count(v.name),
        "party" => session.party.Any(m => m.def.id == v.name) ? 1 : 0,
        "level" => session.party.FirstOrDefault(m => m.def.id == v.name)?.level ?? 0,
        _ => throw new InvalidOperationException(v.kind),
    };

    private static IEnumerable<(string kind, string name)> collect(Node node) => node switch
    {
        Binary b => collect(b.left).Concat(collect(b.right)),
        Not n => collect(n.inner),
        Compare c => collect(c.left).Concat(collect(c.right)),
        Value { kind: "flag" or "item" or "party" or "level" } v => [(v.kind, v.name)],
        _ => [],
    };

    private sealed class Parser
    {
        private readonly string text;
        private int position;

        public Parser(string text)
        {
            this.text = text;
        }

        public Node parseExpression()
        {
            Node left = parseAnd();
            while (accept("||"))
            {
                left = new Binary(left, false, parseAnd());
            }

            return left;
        }

        public void expectEnd()
        {
            skipSpaces();
            if (position < text.Length)
            {
                throw error("unexpected text");
            }
        }

        private Node parseAnd()
        {
            Node left = parseUnary();
            while (accept("&&"))
            {
                left = new Binary(left, true, parseUnary());
            }

            return left;
        }

        private Node parseUnary()
        {
            if (accept("!=", consume: false))
            {
                throw error("expected a value");
            }

            if (accept("!"))
            {
                return new Not(parseUnary());
            }

            if (accept("("))
            {
                Node inner = parseExpression();
                if (!accept(")"))
                {
                    throw error("expected ')'");
                }

                return inner;
            }

            Value left = parseValue();
            foreach (string op in new[] { "==", "!=", ">=", "<=", ">", "<" })
            {
                if (accept(op))
                {
                    return new Compare(left, op, parseValue());
                }
            }

            return left;
        }

        private Value parseValue()
        {
            skipSpaces();
            int start = position;
            if (position < text.Length && (char.IsDigit(text[position]) || text[position] == '-'))
            {
                position++;
                while (position < text.Length && char.IsDigit(text[position]))
                {
                    position++;
                }

                if (!int.TryParse(text.AsSpan(start, position - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int number))
                {
                    throw error("bad number");
                }

                return new Value("number", "", number);
            }

            string word = readIdentifier();
            if (word == "gold")
            {
                return new Value("gold", "", 0);
            }

            if (valuePrefixes.Contains(word) && accept(":"))
            {
                string name = readIdentifier();
                if (name.Length == 0)
                {
                    throw error($"expected a name after '{word}:'");
                }

                return new Value(word, name, 0);
            }

            position = start;
            throw error("expected a number, gold, flag:, item:, party: or level:");
        }

        private string readIdentifier()
        {
            skipSpaces();
            int start = position;
            while (position < text.Length && (char.IsLetterOrDigit(text[position]) || text[position] == '_'))
            {
                position++;
            }

            return text[start..position];
        }

        private bool accept(string token, bool consume = true)
        {
            skipSpaces();
            if (string.CompareOrdinal(text, position, token, 0, token.Length) != 0)
            {
                return false;
            }

            if (consume)
            {
                position += token.Length;
            }

            return true;
        }

        private void skipSpaces()
        {
            while (position < text.Length && char.IsWhiteSpace(text[position]))
            {
                position++;
            }
        }

        private FormatException error(string message) => new($"{message} at {position} in condition \"{text}\"");
    }
}
