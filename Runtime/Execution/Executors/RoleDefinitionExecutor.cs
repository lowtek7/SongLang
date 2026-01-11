using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// 역할 정의 실행: Subject HAS RoleName (Node)
/// </summary>
public sealed class RoleDefinitionExecutor : IStatementExecutor<RoleDefinitionStatement>
{
    public void Execute(RoleDefinitionStatement stmt, ExecutionContext ctx)
    {
        var subject = ctx.Graph.GetOrCreateNode(stmt.Subject);

        // 역할 목록 가져오거나 생성
        var roles = subject.GetInternalProperty("Roles") as List<string>;
        if (roles is null)
        {
            roles = [];
            subject.SetInternalProperty("Roles", roles);
        }

        // 역할 추가 (중복 방지)
        if (!roles.Contains(stmt.RoleName))
        {
            roles.Add(stmt.RoleName);
        }
    }
}
