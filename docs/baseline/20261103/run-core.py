import subprocess,pathlib,os,json,time
root=pathlib.Path.cwd();out=root/'build/baseline-20261103';env={**os.environ,'DOTNET_ROOT':str(root/'.toolcache/dotnet'),'DOTNET_CLI_HOME':str(root/'.toolcache/dotnet-home'),'NUGET_PACKAGES':str(root/'.toolcache/nuget'),'DOTNET_CLI_TELEMETRY_OPTOUT':'1'}
suites=['Core','CityGovernance','CityNetwork','CityArc','WildG0','WildG1','WildTrade','WildContent','RogueContent','Specialization','UI15'];results=[]
for s in suites:
 d=out/'core'/s;d.mkdir(parents=True,exist_ok=True)
 args=[str(root/'content'),str(d)]
 if s in ['CityArc','RogueContent']:args[1]=str(d/'report.json')
 if s=='Specialization':args[1]=str(root/'unity/METLivingWorld/Assets/StreamingAssets/MetContent')
 cmd=[str(root/'.toolcache/dotnet/dotnet'),'run','--project',f'tools/unity_core/{s}.Checks.csproj','--no-restore','--',*args]
 with open(d/'run.log','w') as log:r=subprocess.run(cmd,env=env,stdout=log,stderr=subprocess.STDOUT)
 result={'suite':s,'exit_code':r.returncode,'log':str(d/'run.log')};results.append(result);(out/'core-results.json').write_text(json.dumps(results,indent=2));print(s,r.returncode,flush=True)
