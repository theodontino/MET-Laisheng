using System;
using System.IO;
using MET.Core;
class CityArcProgram {
 static int Main(string[] args) {
  try {var report=CityArcWalkthrough.Execute(new Catalog(args[0]),0,Console.WriteLine);if(args.Length>1)File.WriteAllText(args[1],report.ToJson(true));Console.WriteLine("CITY ARC PASS "+report["checks"].Count+" checks");return 0;}
  catch(Exception error){Console.Error.WriteLine(error);return 1;}
 }
}
