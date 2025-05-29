#!/bin/bash

rsync -vR js/*.js js/float/*.js  html/*.html *.py **/*.py pi@192.168.1.3:~/develop/brew/
