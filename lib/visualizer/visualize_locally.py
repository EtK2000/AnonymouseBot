#!/usr/bin/env python

import os
import re
import sys
import webbrowser


def generate(data, generated_path):
    path = os.path.dirname(__file__)
    template_path = os.path.join(path, 'replay.html.template')
    template = open(template_path, 'r')
    content = template.read()
    template.close()

    base_js_path = "../visualizer/js"
    if not os.path.exists(os.path.join(path, base_js_path)):
        base_js_path = "../../../../app/assets/javascripts/visualizer"

    pirates_data_dir = "../visualizer/data"
    if not os.path.exists(os.path.join(path, pirates_data_dir)):
        pirates_data_dir = "../../../../public/arena/visualizer/data"


    game_template_file = open(os.path.join(path, 'game.html'), 'r')
    game_template = game_template_file.read()
    game_template_file.close()

    path1 = os.path.realpath(__file__)
    path2 = os.path.realpath(generated_path)
    common = os.path.commonprefix((path1, path2))
    path1 = path1[len(common):]
    path2 = path2[len(common):]
    mod_path = '/'.join(['..'] * (path2.count(os.sep)) + [os.path.split(path1)[0].replace('\\', '/')])
    if len(mod_path) > 0 and mod_path[-1] != '/':
        mod_path += '/'

    path_re = re.compile(r"## PATH PLACEHOLDER ##")
    game_template_re = re.compile(r"## GAME TEMPLATE ##")
    base_js_path_re = re.compile(r"## BASE JS PATH ##")
    pirates_data_dir_re = re.compile(r"## PIRATES DATA DIR ##")

    content = path_re.sub(mod_path, content)
    #content = insert_re.sub(data, content)
    content = content.replace('## REPLAY PLACEHOLDER ##', data)
    content = game_template_re.sub(game_template, content)
    content = base_js_path_re.sub(base_js_path, content)
    content = pirates_data_dir_re.sub(pirates_data_dir, content)

    output = open(generated_path, 'w')
    output.write(content)
    output.close()


def launch(filename=None, nolaunch=False, generated_path=None):
    if generated_path == None:
        generated_path = 'replay.html'
    if filename == None:
        data = sys.stdin.read()
        generated_path = os.path.realpath(os.path.join(os.path.dirname(__file__), generated_path))
    else:
        with open(filename, 'r') as f:
            data = f.read()
        generated_path = os.path.join(os.path.split(filename)[0], generated_path)

    generate(data, generated_path)

    # open the page in the browser
    if not nolaunch:
        webbrowser.open('file://'+os.path.realpath(generated_path))

if __name__ == "__main__":
    launch(nolaunch=len(sys.argv) > 1 and sys.argv[1] == '--nolaunch')
