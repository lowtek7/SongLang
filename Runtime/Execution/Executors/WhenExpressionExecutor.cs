using Song.Parser;

namespace Song.Runtime.Execution.Executors;

/// <summary>
/// WHEN 표현식 실행: Subject WHEN (condition) DO ... [ELSE ...] END
/// 표현식이 참이면 본문 실행, 거짓이면 ELSE 실행
/// </summary>
public sealed class WhenExpressionExecutor : IStatementExecutor<WhenExpressionStatement>
{
    public void Execute(WhenExpressionStatement stmt, ExecutionContext ctx)
    {
        // Subject를 컨텍스트에 바인딩 (조건식에서 Subject.Property 접근 가능)
        // ResolveNode를 사용하여 기존 변수 바인딩(Role 등)을 우선 해석
        var subjectNode = ctx.ResolveNode(stmt.Subject);

        // 이미 바인딩된 변수가 없는 경우에만 새로 설정
        var alreadyBound = ctx.Variables.ContainsKey(stmt.Subject);
        if (!alreadyBound && subjectNode is not null)
        {
            ctx.Variables[stmt.Subject] = subjectNode;
        }

        // WHEN subject 설정 (bare 식별자를 속성으로 해석)
        var previousWhenSubject = ctx.WhenSubject;
        ctx.WhenSubject = subjectNode;

        try
        {
            // 조건 평가
            var result = ctx.EvaluateExpression(stmt.Condition);

            if (ctx.IsTruthy(result))
            {
                ctx.Execute(stmt.Body);
            }
            else
            {
                // ELSE WHEN 체이닝
                if (stmt.ElseWhen is not null)
                {
                    Execute(stmt.ElseWhen, ctx);  // 재귀 호출
                }
                // ELSE 블록
                else if (stmt.ElseBody is not null)
                {
                    ctx.Execute(stmt.ElseBody);
                }
            }
        }
        finally
        {
            ctx.WhenSubject = previousWhenSubject;
            if (!alreadyBound && subjectNode is not null)
            {
                ctx.Variables.Remove(stmt.Subject);
            }
        }
    }
}
