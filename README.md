# CLRegAsm [![GitHub license](https://img.shields.io/badge/License-MIT-blue.svg)](https://raw.githubusercontent.com/clechasseur/clregasm/master/LICENSE)

A .NET RegAsm.exe replacement supporting per-user registration. .NET COM libraries that wish to support per-user registration can use the provided `CLRegAsmLib` to detect per-user registration context.

Usage of the command-line client (`CLRegAsm.exe`) should be pretty straightforward since it supports pretty much the same options as `RegAsm.exe`, in addition to the `/user` option to perform a registration for the current user only. Running the client without arguments prints help and lists the options.
