#!/usr/bin/env python3
"""Package a compiled review build, without server config/data overwrite."""
from pathlib import Path
import zipfile,hashlib,json,shutil
r=Path(__file__).resolve().parents[1];out=r/'artifacts';dest=out/'KeranPractice-2.0.0-review';plugin=dest/'game/csgo/addons/counterstrikesharp/plugins/KeranPractice';plugin.mkdir(parents=True,exist_ok=True)
built=r/'src/KeranPractice/bin/Release/net10.0'
for name in ['KeranPractice.dll','KeranPractice.Core.dll','KeranPractice.deps.json']:
 p=built/name
 if not p.is_file():raise SystemExit('Build first: '+str(p))
 shutil.copy2(p,plugin/name)
(plugin/'gamedata').mkdir(exist_ok=True);shutil.copy2(r/'gamedata/keranpractice-v2.json',plugin/'gamedata/keranpractice-v2.json')
# Deliberately ship example separately: extracting ZIP must not overwrite a live config.
example=dest/'examples';example.mkdir(exist_ok=True)
(example/'KeranPractice.json').write_text(json.dumps({'ConfigVersion':2,'Admins':[],'TrainingEnvironmentApproved':False,'PublicPersonalCommands':['noflash','savepos','tp','poslist','delpos','last','back','rethrow','clear','autoclear','nadesupply','savenade','loadnade','nades','delnade','respawn','spawn','tspawn','ctspawn'],'MaxPositionsPerMap':64,'MaxLineupsPerMap':64,'AutoClearDelaySeconds':3,'EnableNativeReplay':True,'MaxTrackedEntities':256},indent=2)+'\n')
for n in ['README.md','VALIDATION-v2.0.md','LICENSE']:shutil.copy2(r/n,dest/n)
shutil.copytree(r/'docs',dest/'docs',dirs_exist_ok=True)
licenses=dest/'licenses';licenses.mkdir(exist_ok=True);shutil.copy2(r/'research/MatchZy-LICENSE.txt',licenses/'MatchZy-LICENSE.txt')
source_files=sorted([*r.glob('src/**/*.cs'),*r.glob('src/**/*.csproj'),r/'gamedata/keranpractice-v2.json'])
source_digest=hashlib.sha256()
for f in source_files:
 if '/obj/' not in str(f) and '/bin/' not in str(f):source_digest.update(f.relative_to(r).as_posix().encode());source_digest.update(f.read_bytes())
(dest/'BUILD-INFO.json').write_text(json.dumps({'Version':'2.0.0-review','SourceContentSHA256':source_digest.hexdigest(),'HumanClientValidated':False,'FormalRelease':False},indent=2)+'\n')
manifest=[]
for p in sorted(dest.rglob('*')):
 if p.is_file() and p.name!='SHA256SUMS.txt':manifest.append(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.relative_to(dest).as_posix())
(dest/'SHA256SUMS.txt').write_text('\n'.join(manifest)+'\n')
zipfile_path=out/'KeranPractice-2.0.0-review-linux.zip'
with zipfile.ZipFile(zipfile_path,'w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(dest.rglob('*')):
  if p.is_file():z.write(p,p.relative_to(out))
print(zipfile_path);print('SHA256',hashlib.sha256(zipfile_path.read_bytes()).hexdigest())
