# Janus ![VSTS Build Status](https://elliotharris23.visualstudio.com/_apis/public/build/definitions/ae1d86b1-9891-4b8d-b0b2-ab9474f784a4/1/badge) <a href="https://scan.coverity.com/projects/elliotharris-janus"><img alt="Coverity Scan Build Status" src="https://scan.coverity.com/projects/11603/badge.svg"/></a>

A simple program to facilitate one way synchronisation of directories.
Given a source directory and a target directory, it will copy any new files added to the source directory to the target directory, and delete any files removed from the source directory in the target directory.

Includes basic filtering support and recursion.

Simple use cases:

 * Backing up to a USB or remote directory
 * Automatically copying built files to a VM
