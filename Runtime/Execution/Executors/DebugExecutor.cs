using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// DEBUG 명령 실행
/// </summary>
public sealed class DebugExecutor : IStatementExecutor<DebugStatement>
{
    public void Execute(DebugStatement stmt, ExecutionContext ctx)
    {
        switch (stmt.Target)
        {
            case DebugTarget.Graph:
                DumpGraph(ctx);
                break;
            case DebugTarget.Tokens:
            case DebugTarget.Ast:
                ctx.Output.WriteLine($"DEBUG {stmt.Target} is not yet implemented.");
                break;
        }
    }

    private static void DumpGraph(ExecutionContext ctx)
    {
        ctx.Output.WriteLine("--- Graph State ---");
        if (ctx.Graph.Count == 0)
        {
            ctx.Output.WriteLine("(empty)");
            return;
        }

        foreach (var node in ctx.Graph.AllNodes)
        {
            ctx.Output.WriteLine(FormatNode(node));
        }
        ctx.Output.WriteLine("-------------------");
    }

    private static string FormatNode(Node node)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"Node({node.Name})");

        if (node.Parents.Count > 0)
        {
            sb.Append($" IS {string.Join(", ", node.Parents.Select(p => p.Name))}");
        }

        // 내부 속성(_로 시작)을 제외한 속성들 출력
        var visibleProps = node.Properties
            .Where(p => !p.Key.StartsWith('_'))
            .ToList();

        if (visibleProps.Count > 0)
        {
            var props = visibleProps.Select(p =>
            {
                var val = p.Value switch
                {
                    string s => $"\"{s}\"",
                    Node n => $"→{n.Name}",
                    _ => p.Value
                };
                return $"{p.Key}={val}";
            });
            sb.Append($" {{ {string.Join(", ", props)} }}");
        }

        // 쿼리 결과 노드인 경우 Items 출력
        var items = node.InternalProperties.TryGetValue("Items", out var it)
            ? it as List<Node>
            : null;
        if (items is { Count: > 0 })
        {
            sb.Append($" CONTAINS [{string.Join(", ", items.Select(n => n.Name))}]");
        }

        // 능력 출력
        var abilities = node.InternalProperties.TryGetValue("Abilities", out var ab)
            ? ab as HashSet<string>
            : null;
        if (abilities is { Count: > 0 })
        {
            sb.Append($" CAN [{string.Join(", ", abilities)}]");
        }

        return sb.ToString();
    }
}
