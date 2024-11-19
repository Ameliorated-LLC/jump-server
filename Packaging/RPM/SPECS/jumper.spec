Name: jumper
Version: 0.1.0
Release: 1%{?dist}
Summary: Secure jump server manager
Group: Applications/System
License: MIT
BuildArch: x86_64
Source0: %{name}-%{version}.tar.gz
Requires: zlib, glibc, iputils, openssh-clients, openssh-server

%description
Secure jump server manager.

%install
rm -rf $RPM_BUILD_ROOT
mkdir -p $RPM_BUILD_ROOT
cp -r * $RPM_BUILD_ROOT/
install -m 0755 usr/bin/jumper $RPM_BUILD_ROOT/usr/bin/jumper

%clean
rm -rf $RPM_BUILD_ROOT

%prep
%autosetup

%post
jumper push

%files
# List the files or directories included in the package
# Example:
# /usr/share/man/man1/jumper.1.gz
# /
# You can use wildcards, e.g., /etc/jumper/*Name: jumper
/usr/bin/jumper
/usr/share/man/man1/jumper.1.gz
/etc/jumper

%define debug_package %{nil}
