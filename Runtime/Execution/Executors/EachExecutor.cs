using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// EACH 실행: Collection EACH Variable DO ... END
/// 컬렉션의 각 항목에 대해 블록 실행
/// </summary>
public sealed class EachExecutor : IStatementExecutor<EachStatement>
{
    public void Execute(EachStatement stmt, ExecutionContext ctx)
    {
        var collectionNode = ctx.Graph.GetNode(stmt.Collection);
        if (collectionNode is null)
        {
            throw new InterpreterException($"Collection '{stmt.Collection}' not found", stmt.Line, stmt.Column);
        }

        // 컬렉션 노드의 자식들 (이 노드를 IS로 상속하는 노드들)
        var children = ctx.Graph.AllNodes
            .Where(n => n.Parents.Contains(collectionNode))
            .ToList();

        foreach (var child in children)
        {
            // 컨텍스트에 변수 바인딩
            ctx.Variables[stmt.Variable] = child;

            ctx.Execute(stmt.Body);

            ctx.Variables.Remove(stmt.Variable);
        }
    }
}
