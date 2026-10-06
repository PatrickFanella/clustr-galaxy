"""Build the reproducible Linux-only telemetry plugin before Unity imports assets."""
import pathlib
import subprocess


def build_native(root):
    root=pathlib.Path(root)
    output=root/"Assets/ClustrGalaxy/Plugins/Linux/x86_64/libclustrtelemetry.so"
    output.parent.mkdir(parents=True,exist_ok=True)
    subprocess.run(["cc","-std=c11","-O2","-fPIC","-shared","-Wall","-Wextra","-Werror",
                    "-fvisibility=hidden",str(root/"native/telemetry.c"),"-ldl","-o",str(output)],check=True)
    return output


if __name__ == "__main__": build_native(pathlib.Path(__file__).resolve().parents[1])
