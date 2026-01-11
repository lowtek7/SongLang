using System.Reflection;
using Song.Parser;

namespace Song.Runtime.Execution;

/// <summary>
/// Statement 타입별 Executor를 관리하는 레지스트리
/// 리플렉션을 통해 IStatementExecutor&lt;T&gt; 구현체를 자동 등록한다.
/// </summary>
public sealed class ExecutorRegistry
{
    private readonly Dictionary<Type, IStatementExecutor> _executors = [];

    public ExecutorRegistry()
    {
        RegisterExecutors();
    }

    /// <summary>
    /// 어셈블리에서 모든 IStatementExecutor&lt;T&gt; 구현체를 찾아 등록
    /// </summary>
    private void RegisterExecutors()
    {
        var executorTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => t.GetInterfaces().Any(IsStatementExecutorInterface));

        foreach (var type in executorTypes)
        {
            var executorInterface = type.GetInterfaces()
                .First(IsStatementExecutorInterface);

            var statementType = executorInterface.GetGenericArguments()[0];
            var executor = (IStatementExecutor)Activator.CreateInstance(type)!;

            _executors[statementType] = executor;
        }
    }

    private static bool IsStatementExecutorInterface(Type type)
    {
        return type.IsGenericType &&
               type.GetGenericTypeDefinition() == typeof(IStatementExecutor<>);
    }

    /// <summary>
    /// Statement 타입에 맞는 Executor 반환
    /// </summary>
    public IStatementExecutor? GetExecutor(Type statementType)
    {
        return _executors.TryGetValue(statementType, out var executor) ? executor : null;
    }

    /// <summary>
    /// Statement 타입에 맞는 Executor 반환 (제네릭 버전)
    /// </summary>
    public IStatementExecutor<T>? GetExecutor<T>() where T : Statement
    {
        return _executors.TryGetValue(typeof(T), out var executor)
            ? executor as IStatementExecutor<T>
            : null;
    }

    /// <summary>
    /// 등록된 Executor 개수
    /// </summary>
    public int Count => _executors.Count;

    /// <summary>
    /// 등록된 모든 Statement 타입
    /// </summary>
    public IEnumerable<Type> RegisteredTypes => _executors.Keys;
}
