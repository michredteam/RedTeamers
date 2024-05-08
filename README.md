# Red Team

This repository is for investigation of Windows process execution techniques.
Most of PoCs are given a name corresponding to the technique.



## Projects

* __[BlockingDLL](./BlockingDLL) :__ This toolset is for testing blocking DLL process. See [README.md](./BlockingDLL/README.md).

* __[CloneProcess](./CloneProcess) :__ This directory is for process forking and reflection. See [README.md](./CloneProcess/README.md).

* __[DarkLoadLibrary](./DarkLoadLibrary/) :__ PoCs in this directory are for testing Dark Load Library which is released See [README.md](./DarkLoadLibrary/README.md)

* __[GhostlyHollowing](./GhostlyHollowing) :__ This PoC performs Ghostly Hollowing.

* __[PhantomDllHollower](./PhantomDllHollower) :__ This PoC performs Phantom DLL Hollowing. See [README.md](./PhantomDllHollower/README.md).

* __[PPIDSpoofing](./PPIDSpoofing) :__ This PoC performs PPID Spoofing.

* __[ProcessDoppelgaenging](./ProcessDoppelgaenging) :__ This PoC performs Process Doppelgänging. Due to kernel protection improvement for Microsoft Defender, this technique does not work for recent Windows OS (since about 2021, maybe). So if you want to test this technique in newer environment, must be stop `Microsoft/Windows Defender Antivirus Service`. 

* __[ProcessGhosting](./ProcessGhosting) :__ This PoC performs Process Ghosting. Due to kernel protection, this technique does not work for newer Windows from 22H2.

* __[ProcessHerpaderping](./ProcessHerpaderping) :__ This PoC performs Process Herpaderping. Due to file lock issue, if you choose a fake image file smaller than you want to execute, file size shrinking will be failed and corrupt file signature for herpaderping process. To take full advantage of this technique, fake image file size should be larger than you want to execute. Due to kernel protection, this technique does not work for newer Windows from 22H2.

* __[ProcessHollowing](./ProcessHollowing) :__ This PoC performs Process Hollowing. 

* __[ReflectiveDLLInjection](./ReflectiveDLLInjection) :__ This toolset is for testing Reflective DLL Injection. See [README.md](./ReflectiveDLLInjection/README.md).

* __[TransactedHollowing](./TransactedHollowing) :__ This PoC performs Transacted Hollowing.





