Goal: Get git-for-windows/msys2-runtime (the MSYS runtime backing Git for  
Windows' bash.exe) to compile from source on Windows, then apply a targeted
patch so it can start up under a Windows AppContainer (LowBox token)       
sandbox, where it currently fails hard at the very first step of process   
initialization.

Background — the exact bug: When bash.exe (linked against msys-2.0.dll) is
launched under a restricted AppContainer token, it fails immediately with:
0 [main] bash (PID) <path>\bash.exe: *** fatal error -                     
NtCreateDirectoryObject(\BaseNamedObjects\msys-2.0S5-<hash>): 0xC0000022   
0xC0000022 is STATUS_ACCESS_DENIED. This has been root-caused (via live    
testing with the real error message, cross-checked against the actual      
source) to get_shared_parent_dir() in winsup/cygwin/shared.cc, called once
during startup from memory_init() → shared_info::create(). That function   
builds a literal path \BaseNamedObjects\<shared_id><...>-<installation_key>
and calls NtCreateDirectoryObject directly — a raw native NT API call, not
the higher-level Win32 CreateMutex/CreateEvent-style APIs that Windows     
automatically redirects into an AppContainer's own private namespace. There
is no fallback logic — any failure goes straight to api_fatal() and the    
process aborts. Windows does not allow AppContainer/LowBox tokens to create
or even look up objects by name in the global \BaseNamedObjects namespace  
at all — this is enforced at the parent-namespace level, so no ACL on the  
target object itself can work around it (confirmed empirically:            
pre-creating the object from an unsandboxed process first, then trying to  
have the sandboxed process reach it, still fails identically).

Suggested fix direction: When running under an AppContainer token, detect  
that (e.g., checking the process token for a package SID via               
GetTokenInformation/TokenAppContainerSid) and construct the shared-object  
path using the documented GetAppContainerNamedObjectPath API (in           
securityappcontainer.h) instead of the literal \BaseNamedObjects\... path,
so the directory object lands in that AppContainer's own private, permitted
namespace. Multiple processes launched under the same AppContainer profile
should still be able to find and share it there.         

Phased approach — do these in order, don't skip ahead:
1. Phase 1 — vanilla build only. Get an unmodified checkout of this repo   
   compiling into a working msys-2.0.dll (+bash.exe if feasible) with zero    
   source changes. This alone is the biggest unknown — the build needs a      
   self-hosted MSYS2/Cygwin-style toolchain (autotools, a matching            
   cross-compiler, specific headers). Use build-extra's documented process as
   your primary reference for how Git for Windows itself builds this. Don't   
   touch a single line of runtime source until this produces a working,       
   testable build. Verify the vanilla build behaves normally in an ordinary   
   (non-sandboxed) shell before moving on.
2. Phase 2 — reproduce the failure independently. Write a small,           
   self-contained AppContainer test harness (a minimal C/C++ or C# program    
   using CreateAppContainerProfile + CreateProcess with                       
   PROC_THREAD_ATTRIBUTE_SECURITY_CAPABILITIES/SECURITY_CAPABILITIES — don't  
   depend on any other project) and confirm your freshly-built vanilla        
   bash.exe/msys-2.0.dll fails with the exact same                            
   NtCreateDirectoryObject/0xC0000022 error under it. This confirms your build
   is a faithful reproduction before you start patching.
3. Phase 3 — patch get_shared_parent_dir() along the lines above, rebuild,
   and re-test under the same harness.
4. Phase 4 — check for more walls. Getting past this one call does not     
   guarantee success — MSYS/Cygwin-lineage runtimes do a lot more low-level NT
   API work for fork() emulation, signal delivery, and pty/console handling,  
   any of which could hit a similar AppContainer-namespace-isolation wall     
   further into startup or into actually running a script. If a bare echo-only
   script now runs cleanly but something more complex doesn't, that's         
   expected — diagnose and report back rather than assuming one patch is the  
   whole fix.

Explicitly out of scope for now: packaging/distribution, replacing the     
user's real installed Git for Windows binaries (don't touch them — work    
only with your own separately-built copy), and any concern about ongoing   
upstream-tracking/maintenance. Just get it compiling, then get it starting
up under a sandboxed AppContainer token. Report back with what you find at
each phase, especially if Phase 1 (the build itself) turns out to be a     
bigger blocker than the actual runtime patch.      