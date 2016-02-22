import subprocess
import unittest
import sys
import os
import logging
from subprocess import Popen


# Add to the system path the folder that include Pirates files
sys.path.append(".")
sys.path.append("..\\")
sys.path.append(os.getcwd() + "\\..\\lib")
sys.path.append(os.getcwd() + "\\..\\bots")

os.chdir("..")
print "Working Dir " , os.getcwd()


def file_len(fname):
    i = 0
    with logging.codecs.open(fname,'r') as f:
        for i, l in enumerate(f):
            pass
    return i + 1

def tail(fname, len):
    file_length = file_len(fname)
    tail = []
    with logging.codecs.open(fname,'r') as f:

        for i, l in enumerate(f):
            if i >= file_length -len:
                tail.append(l)

    return tail

class TestPiratesBot(unittest.TestCase):

    def setUp(self):
        self.myBot = "myBot.py"

    def myBot_vs_Other(self, bots_dir, myBotName, otherBotName):
        # test my bot against demo bot
        cmd =  os.getcwd() + '\\run.bat'
        myBot = bots_dir + myBotName
        otherBot = bots_dir + otherBotName

        folder = os.getcwd() + "\\scripts\\logs\\"
        if not os.path.exists(folder):
            os.makedirs(folder)

        filename = myBotName.rsplit(".")[0] + "_" + otherBotName.rsplit(".")[0]
        f_out_name = ".\\scripts\\logs\\" + filename + "_out.txt"
        f_err_name = ".\\scripts\\logs\\" + filename + "_err.txt"
        f_out = open(f_out_name, 'w')
        f_err = open(f_err_name, 'w')
        map = ".\\maps\\default_map.map"
        flags = "--nolaunch"

        params = [cmd, myBot, otherBot, map, flags]

        with open(f_out_name, 'w') as outfile:
            with open(f_err_name, 'w') as errfile:
                ret = subprocess.call(subprocess.list2cmdline(params), cwd=folder, stdout=outfile, stderr=errfile)
                if ret != 0:
                    if ret < 0: print "Killed by signal", -ret
                    else:       print "Command failed with return code", ret

        if "player 1" in tail(f_out_name,1)[0]:
            return 1
        else:
            return 0

    def test_find_best(self):
        bots_dir = ".\\test\\"
        files = [file for file in os.listdir(bots_dir)]
        #in starter kit
        #files += [".\\python\\" + file for file in os.listdir(".\\bots\\python") if file.endswith(".py")]
        #files += [".\\csharp\\example_bots\\" + file for file in os.listdir(".\\bots\\csharp\\example_bots\\") if file.endswith(".cs")]
        #files += [".\\java\\ChallengeBots\\src\\bots\\" + file for file in os.listdir(".\\bots\\java\\ChallengeBots\\src\\bots\\") if file.endswith(".java")]

        #while developing
        # files += [".\\python\\" + file for file in os.listdir(".\\bots\\python") if file.endswith(".py")]
        # files += [".\\csharp\\example_bots\\" + file for file in os.listdir(".\\bots\\csharp\\example_bots\\") if file.endswith(".cs")]
        # files += [".\\java\\ChallengeBots\\src\\bots\\" + file for file in os.listdir(".\\bots\\java\\ChallengeBots\\src\\bots\\") if file.endswith(".java")]
        #files.append("Demo1.pyc")

        candidates = []
        for candidate in files:
            print "\nCandidate {0}\n*****************".format(candidate)
            wins = 0
            for opponent in files:
                if opponent == candidate: continue
                win = self.myBot_vs_Other(bots_dir, candidate, opponent)
                print "Candidate {0} vs {1} - {2}".format(candidate, opponent, win)
                wins += win
            candidates.append((candidate, wins))
            print candidate + " got " + str(wins) + " wins"

        candidates.sort(key=lambda (candidate, wins): wins, reverse=True)

        print "\nResults\n#######################################################"
        for c in candidates:
            print "Candidate {0} - {1} wins".format(c[0], c[1])


if __name__ == '__main__':
    unittest.main()