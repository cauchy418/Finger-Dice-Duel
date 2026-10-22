"""One-time migration from the legacy 27-role vocabulary to profession/subclass IDs.
Run only against the recorded pre-migration baseline. Keeps existing .meta bytes intact.
"""
import json, re, subprocess, shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SPEC = ROOT / "Art/Production/profession-subclass-v1/migration.json"

def main():
    if (ROOT / 'Assets/Scripts/Core/CharacterIdentity.cs').exists():
        raise RuntimeError('Profession migration already applied; do not rerun this one-time tool.')
    spec = json.loads(SPEC.read_text(encoding="utf-8")); rows = spec["roles"]
    if subprocess.check_output(["git", "rev-parse", "--short", "HEAD"], cwd=ROOT, text=True).strip() != spec["baseline"]:
        raise RuntimeError("This is a one-time migration tied to its baseline commit.")
    tracked = subprocess.check_output(["git", "ls-files", "-z"], cwd=ROOT).decode("utf-8").split("\0")
    tracked = [p for p in tracked if p]
    symbols = {r["oldSymbol"]: r["symbol"] for r in rows}; symbols["Warrior"] = "Breaker"
    ids = {r["oldId"]: r["id"] for r in rows}; ids["priest"] = "cleric_life_domain"
    zh = {r["oldName"]: r["fullName"] for r in rows}
    short_zh = {r["oldName"]: r["subclassName"] for r in rows}
    lower = {r["oldSymbol"][0].lower()+r["oldSymbol"][1:]: r["symbol"][0].lower()+r["symbol"][1:] for r in rows}
    def pattern(keys): return re.compile("|".join(re.escape(k) for k in sorted(keys,key=len,reverse=True)))
    sym_re = pattern(symbols); zh_re = pattern(zh)
    id_re = re.compile(r"(?<=[/\"'`])("+"|".join(map(re.escape,sorted(ids,key=len,reverse=True)))+r")(?=[.\"'/`:]|$)")
    low_re = re.compile(r"\b("+"|".join(map(re.escape,sorted(lower,key=len,reverse=True)))+r")\b")
    def transform(text, compact=False):
        text = id_re.sub(lambda m: ids[m.group(1)],text)
        text = sym_re.sub(lambda m:symbols[m.group()],text)
        text = low_re.sub(lambda m:lower[m.group()],text)
        use = short_zh if compact else zh
        text = zh_re.sub(lambda m:use[m.group()],text)
        text = re.sub(r"\bClasses\b", "Subclasses", text).replace("ClassEmblem","SubclassEmblem")
        text = text.replace("SwashbucklerPowerShot","SwashbucklerDuelistThrust")
        return text
    def new_path(name):
        # Asset and source names are migrated, while dates/record folders remain stable.
        result = sym_re.sub(lambda m:symbols[m.group()],name)
        for old, uid in sorted(ids.items(),key=lambda x:len(x[0]),reverse=True):
            result = re.sub(r"(?<=/)"+re.escape(old)+r"(?=\.)",uid,result)
        result = result.replace("/Classes/","/Subclasses/").replace("/Classes.meta","/Subclasses.meta")
        return result
    # Profession badges intentionally reuse existing suitable painted emblems.
    reuse = {"barbarian":"barbarian","bard":"bard","cleric":"cleric","fighter":"soldier","monk":"monk","paladin":"paladin","ranger":"ranger","rogue":"thief","sorcerer":"sorcerer","warlock":"hexer","wizard":"wizard"}
    folder = ROOT/"Assets/Resources/Art/Battle/v1/Professions"; folder.mkdir(parents=True,exist_ok=True)
    for profession, old in reuse.items(): shutil.copy2(ROOT/f"Assets/Resources/Art/Battle/v1/Classes/{old}.png",folder/f"{profession}.png")
    changed=[]; moves=[]
    for name in tracked:
        file=ROOT/name
        if file.suffix.lower() in {".cs",".cjs",".js",".json",".md",".ps1",".svg"} or name.endswith(".cs.txt"):
            data=file.read_bytes(); source=data.decode("utf-8-sig").replace("\r\n", "\n")
            result=transform(source, name.endswith("/CharacterCodex.cs"))
            if result!=source:
                file.write_text(result,encoding="utf-8-sig" if name.endswith((".cs",".ps1")) or data.startswith(b"\xef\xbb\xbf") else "utf-8")
                changed.append(name)
        elif name.startswith("Tools/") and name.endswith("-report.txt"):
            source=file.read_text(encoding="utf-8-sig"); result=zh_re.sub(lambda m:zh[m.group()],source)
            if result!=source:file.write_text(result,encoding="utf-8");changed.append(name)
        target=new_path(name)
        if target!=name: moves.append((name,target))
    # Every file is moved individually, with resolved path checks; no recursive move/delete.
    for source,target in moves:
        src=(ROOT/source).resolve(); dst=(ROOT/target).resolve()
        if not src.is_relative_to(ROOT) or not dst.is_relative_to(ROOT): raise RuntimeError("Path escaped workspace")
        if dst.exists():raise RuntimeError("Rename collision: "+target)
        dst.parent.mkdir(parents=True,exist_ok=True);src.rename(dst)
    # Keep renamed statistics labels while discarding future random statistical churn.
    report_base=ROOT/"Tools/UiCompileCheck/bin/profession-report-baseline";report_base.mkdir(parents=True,exist_ok=True)
    for p in (ROOT/"Tools").glob("*-report*.txt"):shutil.copy2(p,report_base/p.name)
    audit={"baseline":spec["baseline"],"changedTextFiles":changed,"renamedFiles":[{"from":a,"to":b} for a,b in moves],"professionBadgeReuse":reuse,"metaPolicy":"Existing .meta files moved byte-for-byte, never authored or rewritten."}
    (SPEC.parent/"migration-audit.json").write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding="utf-8")
    print(f"Changed {len(changed)} text files; moved {len(moves)} files with existing metadata preserved; copied 11 profession badges.")
if __name__ == "__main__": main()
