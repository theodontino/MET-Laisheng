using System;
using MET.Core;
class FoundationProgram {
    static int Main(string[] args) {
        try { Console.WriteLine(FoundationChecks.run(args[0]).ToJson());return 0; }
        catch(Exception e) {Console.Error.WriteLine(e);return 1;}
    }
}
