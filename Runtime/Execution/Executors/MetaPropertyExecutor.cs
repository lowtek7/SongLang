using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// 메타 속성 실행: Subject HAS INVERSE/DIRECTION Value
/// </summary>
public sealed class MetaPropertyExecutor : IStatementExecutor<MetaPropertyStatement>
{
    public void Execute(MetaPropertyStatement stmt, ExecutionContext ctx)
    {
        var relationNode = ctx.Graph.GetOrCreateNode(stmt.Subject);

        // 관계가 RELATION인지 확인
        if (!relationNode.Is("RELATION"))
        {
            throw new InterpreterException(
                $"'{stmt.Subject}' is not a relation. IS RELATION must be declared first.",
                stmt.Line, stmt.Column);
        }

        switch (stmt.Type)
        {
            case MetaPropertyType.Inverse:
                ExecuteInverse(stmt, relationNode, ctx);
                break;

            case MetaPropertyType.Direction:
                ExecuteDirection(stmt, relationNode);
                break;
        }
    }

    private static void ExecuteInverse(MetaPropertyStatement stmt, Node relationNode, ExecutionContext ctx)
    {
        // 역관계 이름 저장
        relationNode.SetInternalProperty("Inverse", stmt.Value);

        // 역관계 노드 자동 생성
        var inverseNode = ctx.Graph.GetOrCreateNode(stmt.Value);
        if (!inverseNode.Is("RELATION"))
        {
            var relationParent = ctx.Graph.GetOrCreateNode("RELATION");
            inverseNode.AddParent(relationParent);
        }

        // 역방향 참조 저장
        inverseNode.SetInternalProperty("InverseOf", stmt.Subject);

        // 역할 복사 (순서 교환)
        var roles = relationNode.GetInternalProperty("Roles") as List<string>;
        if (roles is not null && roles.Count >= 2)
        {
            var inverseRoles = new List<string> { roles[1], roles[0] };
            if (roles.Count > 2)
            {
                inverseRoles.AddRange(roles.Skip(2));
            }
            inverseNode.SetInternalProperty("Roles", inverseRoles);
        }
    }

    private static void ExecuteDirection(MetaPropertyStatement stmt, Node relationNode)
    {
        var dirValue = stmt.Value.ToUpperInvariant();
        relationNode.SetInternalProperty("Direction", dirValue);

        if (dirValue == "BIDIRECTIONAL")
        {
            relationNode.SetInternalProperty("Bidirectional", true);
        }
    }
}
