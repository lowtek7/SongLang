using Song.Parser;

namespace Song.Runtime.Execution;

/// <summary>
/// Statement 실행기 인터페이스 (비제네릭 기본)
/// </summary>
public interface IStatementExecutor
{
    /// <summary>
    /// Statement 실행
    /// </summary>
    void Execute(Statement statement, ExecutionContext context);
}

/// <summary>
/// 타입별 Statement 실행기 인터페이스
/// 각 Statement 타입에 대한 실행 로직을 구현한다.
/// </summary>
/// <typeparam name="T">처리할 Statement 타입</typeparam>
public interface IStatementExecutor<in T> : IStatementExecutor where T : Statement
{
    /// <summary>
    /// 타입별 Statement 실행
    /// </summary>
    void Execute(T statement, ExecutionContext context);

    /// <summary>
    /// 비제네릭 인터페이스 구현 (기본 구현)
    /// </summary>
    void IStatementExecutor.Execute(Statement statement, ExecutionContext context)
        => Execute((T)statement, context);
}
