# Song

A relation-centric programming language where everything is a node and relationships define behavior.

**[Try it online](https://lowtek7.github.io/SongLangTS/)**

```
Player IS Entity
Player HAS HP 100
Player HAS Name "Hero"

Enemy IS Entity
Enemy HAS HP 50

Attack IS RELATION
Attack HAS Attacker (Node)
Attack HAS Victim (Node)
Attack DO
    Victim HAS HP (Victim.HP - Attacker.Damage)
END

Player HAS Damage 25
Player Attack Enemy    // Enemy.HP is now 25
```

## Philosophy

Song takes a different approach from traditional OOP:

```
OOP:  player.Attack(enemy)     // behavior belongs to object
Song: Player Attack Enemy      // behavior belongs to relationship
```

Core principles:
- **Everything is a node** - data, types, relationships, behaviors
- **Nodes are collections of relationships** - identity emerges from relationships
- **No type/instance distinction** - prototype-based, any node can be specialized
- **Code = Graph** - everything can be represented as a graph

## Getting Started

### Requirements

- .NET 10.0+

### Build & Run

```bash
# Build
dotnet build

# Run a file
dotnet run -- yourfile.song

# Start REPL
dotnet run
```

### REPL Commands

```
:help, :h        Show help
:help <keyword>  Detailed help for keyword
:graph, :g       Dump current graph state
:clear, :c       Reset graph
:quit, :q        Exit
```

## Syntax

### Basic Relations

```
// Inheritance (IS)
Player IS Entity
Dragon IS Monster
Dragon IS Flying        // multiple inheritance

// Properties (HAS)
Player HAS HP 100
Player HAS Name "Hero"
Player HAS Damage (10 + Level * 2)    // expressions

// Property access
Player.HP PRINT              // dot notation
(HP OF Player) PRINT         // OF notation

// Abilities (CAN)
Player CAN ATTACK
Bird CAN FLY

// Remove (LOSES)
Player LOSES HP              // remove property
Player LOSES FLY             // remove ability
Player LOSES IS Monster      // remove inheritance

// Collection (CONTAINS / IN)
Inventory CONTAINS Sword     // Inventory has Sword as child
Potion IN Inventory          // same as: Inventory CONTAINS Potion

// Count children
Inventory.COUNT PRINT        // number of children

// Clear all children
Inventory CLEAR              // remove all children
```

### Control Flow

```
// Conditional (WHEN)
Player WHEN (HP < 30) DO
    Player HAS Status "Critical"
END

// With ELSE
Player WHEN (HP > 70) DO
    Player HAS Grade "A"
ELSE WHEN (HP > 40) DO
    Player HAS Grade "B"
ELSE DO
    Player HAS Grade "C"
END

// Pattern-based condition
Player HAS HP 0 WHEN DO
    Player IS Dead
END

// Loop over children (EACH)
Inventory EACH Item DO
    Item PRINT
END

// Apply to all matching nodes (ALL)
ALL Enemy HAS Stunned true
ALL Monster PRINT
```

### Probability

```
// Random number (inclusive)
Player HAS Damage (RANDOM 10 30)

// Chance block
CHANCE 30 DO
    Player HAS CriticalHit true
ELSE DO
    Player HAS CriticalHit false
END

// Dynamic probability
CHANCE (Player.Luck * 2) DO
    Player HAS DoubleReward true
END
```

### Custom Relations

```
// Define a relation with roles
Attack IS RELATION
Attack HAS Attacker (Node)    // first role = caller
Attack HAS Victim (Node)      // second role = target
Attack DO
    Victim HAS HP (Victim.HP - Attacker.Damage)
END

// Use it
Player Attack Enemy

// Inline role definition (shorter syntax)
Heal IS RELATION (Healer, Target)
Heal DO
    Target HAS HP (Target.HP + Healer.HealPower)
END

// Three-role relation
Trade IS RELATION (Giver, Receiver, Item)
Trade DO
    Giver LOSES CONTAINS Item
    Receiver CONTAINS Item
END

Player Trade Merchant Sword
```

### Return Values (GIVES)

```
// Relations can return values
GetDamage IS RELATION (Source)
GetDamage DO
    GIVES Source.BaseDamage + Source.BonusDamage
END

// Use return value in expression
Enemy HAS HP (Enemy.HP - (Player GetDamage))

// Use in calculations
TotalDamage HAS Value ((Attacker GetDamage) * CritMultiplier)
```

### Meta Relations

Define meta-properties on relations for automatic inverse and bidirectional behavior.

```
// INVERSE: Auto-create reverse relationship
OWNS IS RELATION
OWNS HAS Owner (Node)
OWNS HAS Item (Node)
OWNS HAS INVERSE OWNED_BY      // Creates OWNED_BY relation automatically

OWNS DO
    Owner HAS Inventory Item
END

Player OWNS Sword              // Also creates: Sword OWNED_BY Player

// Query relationships
Player OWNS ?                  // What does Player own? -> Sword
? OWNS Sword                   // Who owns Sword? -> Player


// BIDIRECTIONAL: Symmetric relationship
KNOWS IS RELATION
KNOWS HAS Person1 (Node)
KNOWS HAS Person2 (Node)
KNOWS HAS DIRECTION BIDIRECTIONAL

Alice KNOWS Bob                // Also creates: Bob KNOWS Alice

Alice KNOWS ?                  // -> Bob
Bob KNOWS ?                    // -> Alice
```

### Query System

```
// Find nodes by type
?enemies IS Enemy

// Find by property
?wounded HAS HP WHERE ?wounded.HP < 50

// Find by ability
?flyers CAN FLY

// Find by containment
?items IN Inventory          // nodes contained in Inventory
?containers CONTAINS         // nodes that have children

// Use query results with EACH
?items IN Inventory
items EACH Item DO
    Item PRINT
END

// Use query results with ALL
?weakEnemies IS Enemy WHERE ?weakEnemies.HP < 30
ALL ?weakEnemies HAS Marked true
```

### Expressions

```
// Arithmetic: + - * / %
Player HAS TotalDamage (BaseDamage + BonusDamage)

// Comparison: == != < > <= >=
Player WHEN (HP > MaxHP / 2) DO ... END

// Logical: AND OR NOT
Player WHEN (HP > 0 AND Level > 5) DO ... END
Player WHEN (NOT IsDead) DO ... END

// String concatenation
Message HAS Text ("HP: " + Player.HP)
```

### Debug

```
// Dump entire graph
DEBUG GRAPH

// Output
// --- Graph State ---
// Node(Player) IS Entity { HP=100, Name="Hero" } CAN [ATTACK]
// Node(Entity)
// -------------------
```

## Project Structure

```
Song/
├── Program.cs           # Entry point, REPL
├── Tokenizer/
│   ├── Token.cs         # Token type and value
│   ├── TokenType.cs     # Token type enum
│   └── Tokenizer.cs     # Lexer
├── Parser/
│   ├── Statement.cs     # AST statement nodes
│   ├── Expression.cs    # AST expression nodes
│   └── Parser.cs        # Parser
├── Runtime/
│   ├── Node.cs          # Node representation
│   ├── Graph.cs         # Node/relationship storage
│   ├── Interpreter.cs   # Execution engine
│   └── SongError.cs     # Error types
└── Repl/
    └── HelpSystem.cs    # REPL help
```

## Examples

See test files for more examples:
- `test.song` - Basic syntax
- `test2.song` - CONTAINS and EACH
- `test_roles.song` - Custom relations with roles
- `test_query.song` - Query system
- `test_else.song` - Conditional branching
- `test_random.song` - RANDOM and CHANCE
- `test_meta_relation.song` - INVERSE and BIDIRECTIONAL relations
- `test_new_features.song` - COUNT, CLEAR, IN query

## License

MIT

## Author

Amber Song
