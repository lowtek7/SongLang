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
        // 자신의 속성 먼저 확인
        if (Properties.TryGetValue(name, out var value))
        {
            return value;
        }

        // 부모 노드들에서 찾기 (프로토타입 상속)
        foreach (var parent in Parents)
        {
            var parentValue = parent.GetProperty(name);
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
        if (Name == typeName) return true;

        foreach (var parent in Parents)
        {
            if (parent.Is(typeName))
            {
                return true;
            }
        }

        return false;
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
