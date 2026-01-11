namespace Song.Runtime;

/// <summary>
/// Song 언어의 노드
/// 노드는 관계의 집합이다. 이름과 속성들을 가진다.
/// </summary>
public sealed class Node
{
    public string Name { get; }

    /// <summary>
    /// IS 관계로 연결된 부모 노드들 (프로토타입 체인)
    /// </summary>
    public List<Node> Parents { get; } = [];

    /// <summary>
    /// HAS 관계로 연결된 속성들 (Property -> Value)
    /// </summary>
    public Dictionary<string, object?> Properties { get; } = [];

    public Node(string name)
    {
        Name = name;
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
    /// 부모 노드 추가 (IS 관계)
    /// </summary>
    public void AddParent(Node parent)
    {
        if (!Parents.Contains(parent))
        {
            Parents.Add(parent);
        }
    }

    /// <summary>
    /// 부모 노드 제거 (LOSES IS)
    /// </summary>
    public void RemoveParent(Node parent)
    {
        Parents.Remove(parent);
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
        var instances = GetProperty("_RelationInstances") as List<RelationInstance>;
        if (instances is null)
        {
            instances = [];
            SetProperty("_RelationInstances", instances);
        }
        instances.Add(instance);
    }

    /// <summary>
    /// 관계 인스턴스 조회 (특정 관계명 또는 전체)
    /// </summary>
    public List<RelationInstance> GetRelationInstances(string? relationName = null)
    {
        var instances = GetProperty("_RelationInstances") as List<RelationInstance>;
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
