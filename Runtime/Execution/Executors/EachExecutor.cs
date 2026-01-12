using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// EACH 실행: Collection EACH Variable DO ... END
/// 컬렉션의 Children(CONTAINS 관계)을 순회
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

        // 컬렉션 노드의 Children (CONTAINS 관계로 포함된 노드들)
        foreach (var child in collectionNode.Children)
        {
            // 컨텍스트에 변수 바인딩
            ctx.Variables[stmt.Variable] = child;

            ctx.Execute(stmt.Body);

            ctx.Variables.Remove(stmt.Variable);
        }
    }
}
