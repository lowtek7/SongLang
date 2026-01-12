namespace Song.Runtime;

/// <summary>
/// Song 언어의 그래프
/// 모든 노드들과 관계를 저장한다.
/// </summary>
public sealed class Graph
{
    private readonly Dictionary<string, Node> _nodes = [];

    /// <summary>
    /// 타입 인덱스: 특정 타입(IS 관계)의 모든 노드를 빠르게 조회
    /// Key: 타입명, Value: 해당 타입의 노드들
    /// </summary>
    private readonly Dictionary<string, HashSet<Node>> _typeIndex = [];

    /// <summary>
    /// 관계 역인덱스: 특정 관계의 Target으로부터 Source를 빠르게 조회
    /// Key: (관계명, Target 노드), Value: Source 노드들
    /// </summary>
    private readonly Dictionary<(string, Node), HashSet<Node>> _relationReverseIndex = [];

    /// <summary>
    /// 노드 가져오기 (없으면 생성)
    /// </summary>
    public Node GetOrCreateNode(string name)
    {
        if (!_nodes.TryGetValue(name, out var node))
        {
            node = new Node(name, this);  // Graph 참조 전달
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

    #region 타입 인덱스

    /// <summary>
    /// 특정 타입의 직접 자식 노드 조회 O(1)
    /// 프로토타입 체인 고려 안함
    /// </summary>
    public IEnumerable<Node> GetNodesByType(string typeName)
    {
        return _typeIndex.TryGetValue(typeName, out var nodes) ? nodes : [];
    }

    /// <summary>
    /// 특정 타입의 모든 노드 조회 (프로토타입 체인 포함)
    /// 예: Entity의 자식 Player가 있고, Player의 자식 Hero가 있으면
    /// GetAllNodesByType("Entity")는 Player와 Hero 모두 반환
    /// </summary>
    public IEnumerable<Node> GetAllNodesByType(string typeName)
    {
        var result = new HashSet<Node>();
        var toProcess = new Queue<string>();
        toProcess.Enqueue(typeName);

        while (toProcess.Count > 0)
        {
            var type = toProcess.Dequeue();
            foreach (var node in GetNodesByType(type))
            {
                if (result.Add(node))
                {
                    // 이 노드도 다른 노드의 부모일 수 있으므로 처리 대기열에 추가
                    toProcess.Enqueue(node.Name);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 노드를 타입 인덱스에 등록 (Node.AddParent에서 호출)
    /// </summary>
    internal void RegisterNodeType(Node node, string typeName)
    {
        if (!_typeIndex.TryGetValue(typeName, out var nodes))
        {
            nodes = [];
            _typeIndex[typeName] = nodes;
        }
        nodes.Add(node);
    }

    /// <summary>
    /// 노드를 타입 인덱스에서 제거 (Node.RemoveParent에서 호출)
    /// </summary>
    internal void UnregisterNodeType(Node node, string typeName)
    {
        if (_typeIndex.TryGetValue(typeName, out var nodes))
        {
            nodes.Remove(node);
            if (nodes.Count == 0)
            {
                _typeIndex.Remove(typeName);
            }
        }
    }

    #endregion

    #region 관계 역인덱스

    /// <summary>
    /// 특정 관계의 Target으로부터 Source 노드들 조회 O(1)
    /// 예: GetSourceNodes("OWNS", Sword) -> Sword를 소유한 노드들
    /// </summary>
    public IEnumerable<Node> GetSourceNodes(string relationName, Node target)
    {
        var key = (relationName.ToUpperInvariant(), target);
        return _relationReverseIndex.TryGetValue(key, out var sources) ? sources : [];
    }

    /// <summary>
    /// 관계를 역인덱스에 등록 (Node.AddRelationInstance에서 호출)
    /// </summary>
    internal void RegisterRelation(Node source, string relationName, Node target)
    {
        var key = (relationName.ToUpperInvariant(), target);
        if (!_relationReverseIndex.TryGetValue(key, out var sources))
        {
            sources = [];
            _relationReverseIndex[key] = sources;
        }
        sources.Add(source);
    }

    /// <summary>
    /// 관계를 역인덱스에서 제거
    /// </summary>
    internal void UnregisterRelation(Node source, string relationName, Node target)
    {
        var key = (relationName.ToUpperInvariant(), target);
        if (_relationReverseIndex.TryGetValue(key, out var sources))
        {
            sources.Remove(source);
            if (sources.Count == 0)
            {
                _relationReverseIndex.Remove(key);
            }
        }
    }

    #endregion
}
