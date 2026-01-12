namespace Song.Runtime;

/// <summary>
/// Song 언어의 노드
/// 노드는 관계의 집합이다. 이름과 속성들을 가진다.
/// </summary>
public sealed class Node
{
    public string Name { get; }

    /// <summary>
    /// 소속된 그래프 (인덱스 업데이트용)
    /// </summary>
    internal Graph? Graph { get; }

    /// <summary>
    /// IS 관계로 연결된 부모 노드들 (프로토타입 체인)
    /// </summary>
    public List<Node> Parents { get; } = [];

    /// <summary>
    /// CONTAINS 관계로 연결된 자식 노드들 (컬렉션)
    /// </summary>
    public List<Node> Children { get; } = [];

    /// <summary>
    /// HAS 관계로 연결된 속성들 (Property -> Value)
    /// </summary>
    public Dictionary<string, object?> Properties { get; } = [];

    /// <summary>
    /// 인터프리터 내부 전용 속성들 (사용자 코드에서 접근 불가)
    /// Roles, DoBody, Abilities, RelationInstances 등
    /// </summary>
    internal Dictionary<string, object?> InternalProperties { get; } = [];

    public Node(string name, Graph? graph = null)
    {
        Name = name;
        Graph = graph;
    }

    /// <summary>
    /// 속성 값 가져오기 (프로토타입 체인 탐색)
    /// </summary>
    public object? GetProperty(string name)
    {
        return GetPropertyWithVisited(name, []);
    }

    private object? GetPropertyWithVisited(string name, HashSet<Node> visited)
    {
        // 순환 참조 방어
        if (!visited.Add(this))
        {
            return null;
        }

        // 자신의 속성 먼저 확인
        if (Properties.TryGetValue(name, out var value))
        {
            return value;
        }

        // 부모 노드들에서 찾기 (프로토타입 상속)
        foreach (var parent in Parents)
        {
            var parentValue = parent.GetPropertyWithVisited(name, visited);
            if (parentValue is not null)
            {
                return parentValue;
            }
        }

        return null;
    }

    /// <summary>
    /// 속성 값 설정하기
    /// </summary>
    public void SetProperty(string name, object? value)
    {
        Properties[name] = value;
    }

    /// <summary>
    /// 내부 속성 값 가져오기 (프로토타입 상속 없음)
    /// </summary>
    internal object? GetInternalProperty(string name)
    {
        return InternalProperties.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>
    /// 내부 속성 값 설정하기
    /// </summary>
    internal void SetInternalProperty(string name, object? value)
    {
        InternalProperties[name] = value;
    }

    /// <summary>
    /// 부모 노드 추가 (IS 관계)
    /// </summary>
    public void AddParent(Node parent)
    {
        if (!Parents.Contains(parent))
        {
            Parents.Add(parent);
            Graph?.RegisterNodeType(this, parent.Name);
        }
    }

    /// <summary>
    /// 부모 노드 제거 (LOSES IS)
    /// </summary>
    public void RemoveParent(Node parent)
    {
        if (Parents.Remove(parent))
        {
            Graph?.UnregisterNodeType(this, parent.Name);
        }
    }

    /// <summary>
    /// 자식 노드 추가 (CONTAINS 관계)
    /// </summary>
    public void AddChild(Node child)
    {
        if (!Children.Contains(child))
        {
            Children.Add(child);
        }
    }

    /// <summary>
    /// 자식 노드 제거 (LOSES CONTAINS)
    /// </summary>
    public void RemoveChild(Node child)
    {
        Children.Remove(child);
    }

    /// <summary>
    /// 자신의 속성인지 확인 (상속 제외)
    /// </summary>
    public bool HasOwnProperty(string name)
    {
        return Properties.ContainsKey(name);
    }

    /// <summary>
    /// 속성 제거
    /// </summary>
    public void RemoveProperty(string name)
    {
        Properties.Remove(name);
    }

    /// <summary>
    /// 특정 타입인지 확인 (IS 관계 체인 탐색)
    /// </summary>
    public bool Is(string typeName)
    {
        return IsWithVisited(typeName, []);
    }

    private bool IsWithVisited(string typeName, HashSet<Node> visited)
    {
        // 순환 참조 방어
        if (!visited.Add(this))
        {
            return false;
        }

        if (Name == typeName) return true;

        foreach (var parent in Parents)
        {
            if (parent.IsWithVisited(typeName, visited))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 관계 인스턴스 추가
    /// </summary>
    public void AddRelationInstance(RelationInstance instance)
    {
        var instances = GetInternalProperty("RelationInstances") as List<RelationInstance>;
        if (instances is null)
        {
            instances = [];
            SetInternalProperty("RelationInstances", instances);
        }
        instances.Add(instance);

        // 역인덱스 업데이트 (역관계가 아닌 경우만)
        if (!instance.IsInverse)
        {
            Graph?.RegisterRelation(this, instance.RelationName, instance.Target);
        }
    }

    /// <summary>
    /// 관계 인스턴스 조회 (특정 관계명 또는 전체)
    /// </summary>
    public List<RelationInstance> GetRelationInstances(string? relationName = null)
    {
        var instances = GetInternalProperty("RelationInstances") as List<RelationInstance>;
        if (instances is null) return [];

        if (relationName is null) return instances.ToList();
        return instances.Where(i => i.RelationName.Equals(relationName, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public override string ToString()
    {
        var props = string.Join(", ", Properties.Select(p =>
        {
            var val = p.Value is string s ? $"\"{s}\"" : p.Value;
            return $"{p.Key}={val}";
        }));

        var parents = Parents.Count > 0
            ? $" IS {string.Join(", ", Parents.Select(p => p.Name))}"
            : "";

        return $"Node({Name}{parents}) {{ {props} }}";
    }
}

/// <summary>
/// 관계 인스턴스
/// 두 노드 간의 관계 연결을 나타낸다.
/// </summary>
public sealed class RelationInstance
{
    public string RelationName { get; }
    public Node Target { get; }
    public bool IsInverse { get; }
    public string? OriginalRelation { get; }

    public RelationInstance(string relationName, Node target, bool isInverse = false, string? originalRelation = null)
    {
        RelationName = relationName;
        Target = target;
        IsInverse = isInverse;
        OriginalRelation = originalRelation;
    }

    public override string ToString() => $"{RelationName} -> {Target.Name}";
}
