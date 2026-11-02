
set glob = CreateObject("Chilkat.Global")
MsgBox glob.Version

success = glob.UnlockBundle("30-day Trial")
MsgBox glob.LastErrorText



