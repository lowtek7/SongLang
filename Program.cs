using Song.Tokenizer;
using Song.Parser;
using Song.Runtime;
using Song.Repl;

// Song 언어 인터프리터
// 사용법: Song.exe [파일경로]
// 파일 없이 실행하면 REPL 모드

if (args.Length > 0)
{
    RunFile(args[0]);
}
else
{
    RunRepl();
}

void RunFile(string path)
{
    if (!File.Exists(path))
    {
        Console.WriteLine($"파일을 찾을 수 없습니다: {path}");
        return;
    }

    string source = File.ReadAllText(path);
    var interpreter = new Interpreter();

    try
    {
        var tokenizer = new Tokenizer(source);
        var tokens = tokenizer.Tokenize();

        var parser = new Parser(tokens);
        var statements = parser.Parse();

        interpreter.Execute(statements);
    }
    catch (TokenizerException ex)
    {
        Console.WriteLine($"Tokenizer Error: {ex.Message}");
    }
    catch (ParserException ex)
    {
        Console.WriteLine($"Parser Error: {ex.Message}");
    }
    catch (SongError ex)
    {
        Console.WriteLine($"Runtime Error: {ex.Message}");
    }
}

void RunRepl()
{
    Console.WriteLine("Song REPL v0.3");
    Console.WriteLine("Type :help for commands, :quit to exit\n");

    var interpreter = new Interpreter();

    while (true)
    {
        Console.Write("> ");
        string? input = Console.ReadLine();

        if (input is null)
        {
            break;
        }

        input = input.Trim();

        if (string.IsNullOrEmpty(input))
        {
            continue;
        }

        // REPL 명령어 처리
        if (input.StartsWith(':'))
        {
            var result = HandleReplCommand(input, ref interpreter);
            if (result == ReplCommandResult.Quit)
            {
                break;
            }
            continue;
        }

        // Song 코드 실행
        ExecuteLine(input, interpreter);
    }

    Console.WriteLine("Bye!");
}

ReplCommandResult HandleReplCommand(string command, ref Interpreter interpreter)
{
    // 명령어와 인자 분리
    var parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
    string cmd = parts[0].ToLowerInvariant();
    string? arg = parts.Length > 1 ? parts[1].Trim() : null;

    switch (cmd)
    {
        case ":quit":
        case ":exit":
        case ":q":
            return ReplCommandResult.Quit;

        case ":help":
        case ":h":
            if (arg is not null)
            {
                HelpSystem.PrintTopic(arg);
            }
            else
            {
                HelpSystem.PrintOverview();
            }
            break;

        case ":graph":
        case ":g":
            interpreter.DumpGraph();
            break;

        case ":clear":
        case ":c":
            interpreter = new Interpreter();
            Console.WriteLine("그래프가 초기화되었습니다.");
            break;

        default:
            Console.WriteLine($"알 수 없는 명령어: {command}");
            Console.WriteLine("사용 가능한 명령어를 보려면 :help를 입력하세요.");
            break;
    }

    return ReplCommandResult.Continue;
}

void ExecuteLine(string line, Interpreter interpreter)
{
    try
    {
        var tokenizer = new Tokenizer(line);
        var tokens = tokenizer.Tokenize();

        var parser = new Parser(tokens);
        var statements = parser.Parse();

        interpreter.Execute(statements);
    }
    catch (TokenizerException ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
    catch (ParserException ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
    catch (SongError ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}

enum ReplCommandResult
{
    Continue,
    Quit
}
