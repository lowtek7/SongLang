namespace Song.Runtime;

/// <summary>
/// Song 언어의 그래프
/// 모든 노드들과 관계를 저장한다.
/// </summary>
public sealed class Graph
{
    private readonly Dictionary<string, Node> _nodes = [];

    /// <summary>
    /// 노드 가져오기 (없으면 생성)
    /// </summary>
    public Node GetOrCreateNode(string name)
    {
        if (!_nodes.TryGetValue(name, out var node))
        {
            node = new Node(name);
            _nodes[name] = node;
        }
        return node;
    }

    /// <summary>
    /// 노드 가져오기 (없으면 null)
    /// </summary>
    public Node? GetNode(string name)
    {
        return _nodes.TryGetValue(name, out var node) ? node : null;
    }

    /// <summary>
    /// 노드 존재 여부 확인
    /// </summary>
    public bool HasNode(string name)
    {
        return _nodes.ContainsKey(name);
    }

    /// <summary>
    /// 모든 노드들
    /// </summary>
    public IEnumerable<Node> AllNodes => _nodes.Values;

    /// <summary>
    /// 노드 개수
    /// </summary>
    public int Count => _nodes.Count;

    /// <summary>
    /// 그래프를 문자열로 출력
    /// </summary>
    public override string ToString()
    {
        var lines = _nodes.Values.Select(n => n.ToString());
        return string.Join("\n", lines);
    }
}
