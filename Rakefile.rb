# Define constants for file locations etc
HFM_LIB         = 'lib/hfm-11.1.2.2'
LOG4NET35_LIB   = 'lib\log4net-1.2.11\bin\net\3.5\release'
LOG4NET40_LIB   = 'lib\log4net-1.2.11\bin\net\4.0\release'
FRAMEWORK35_DIR = 'C:\WINDOWS\Microsoft.NET\Framework\v3.5'
FRAMEWORK40_DIR = 'C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319'

BUILD_DIR       = 'gen'
RELEASE_DIR     = 'bin'
PACKAGE_DIR     = 'package'

BUILD35_DIR     = "#{BUILD_DIR}/3.5"
RELEASE35_DIR   = "#{RELEASE_DIR}/3.5"
HFMCMD35_EXE    = "#{BUILD35_DIR}/HFMCmd.exe"
HFMCMD35_BUNDLE = "#{RELEASE35_DIR}/HFMCmd.exe"

BUILD40_DIR     = "#{BUILD_DIR}/4.0"
RELEASE40_DIR   = "#{RELEASE_DIR}/4.0"
HFMCMD40_EXE    = "#{BUILD40_DIR}/HFMCmd.exe"
HFMCMD40_BUNDLE = "#{RELEASE40_DIR}/HFMCmd.exe"

RESOURCES       = FileList['resources/*.resx']
PROPERTIES      = FileList['properties/*']
SOURCE_FILES    = FileList['src/**/*.cs']

PACKAGE_FILES  = ['README.md', 'LICENSE', 'HISTORY']


def settings_for_version(version)
  s = {}
  case version
  when "3.5"
    s[:exe]     = HFMCMD35_EXE.gsub('/', '\\')
    s[:bundle]  = HFMCMD35_BUNDLE.gsub('/', '\\')
    s[:log4net] = LOG4NET35_LIB
    s[:dotnet]  = FRAMEWORK35_DIR
    s[:hfm_ref] = "/reference"
    s[:ilmerge] = "v2"
  when "4.0"
    s[:exe]     = HFMCMD40_EXE.gsub('/', '\\')
    s[:bundle]  = HFMCMD40_BUNDLE.gsub('/', '\\')
    s[:log4net] = LOG4NET40_LIB
    s[:dotnet]  = FRAMEWORK40_DIR
    s[:hfm_ref] = "/link"
    s[:ilmerge] = "v4"
  else
    raise "Invalid .NET version number"
  end
  s
end


def increment_build
  require 'erb'

  commits = `git log --oneline`
  commits = commits.split("\n")
  @build_num = commits.size
  @git_hash = commits[0].split(' ')[0]
  template = File.read('properties\AssemblyInfo.cs.erb')
  erb = ERB.new(template)
  File.open('gen\AssemblyInfo.cs', "w") do |f|
    f.puts erb.result
  end
  puts "Build is now: #{@build_num}"
end


def get_version
  f = File.new('properties\AssemblyInfo.cs.erb', 'r')
  begin
    f.each_line do |line|
      if line =~ /AssemblyVersion\("(\d+\.\d+\.\d+)/
        return $1
      end
    end
  ensure
    f.close
  end
end


def compile_resource(source)
  name = File.basename(source, '.resx')
  "tools\\ResGen.exe #{source} gen\\HFMCmd.Resource.#{name}.resources /str:cs,HFMCmd.Resource,#{name},gen\\#{name}Resource.cs"
end


def compile(version)
  s = settings_for_version(version)
  options = "/nologo /target:exe /main:HFMCmd.Launcher /out:#{s[:exe]} /debug /optimize+"
  log4net_ref = "/lib:#{s[:log4net]} /reference:log4net.dll"
  fso_ref = "/lib:lib /reference:Interop.SCRIPTINGLib.dll"
  hfm = ["/lib:#{HFM_LIB}"]
  FileList["#{HFM_LIB}/*.dll"].each do |dll|
    hfm << "#{s[:hfm_ref]}:#{File.basename(dll)}"
  end
  resources = FileList['gen/*.resources'].map{ |f| "/resource:#{f.gsub('/', '\\')}" }
  source = "src\\*.cs src\\command\\*.cs src\\commandline\\*.cs src\\yaml\\*.cs src\\hfm\\*.cs gen\\*.cs"

  "#{s[:dotnet]}\\csc.exe #{options} #{log4net_ref} #{fso_ref} #{hfm.join(' ')} #{resources.join(' ')} #{source}"
end


# Bundles all log4net and HFM interop .dlls into the HFMCmd.exe,
# so that HFMCmd can be distributed as a single .exe
def bundle(version)
  s = settings_for_version(version)
  tgt = version == "3.5" ? '' : "/targetplatform:#{s[:ilmerge]},#{s[:dotnet]}"

  "tools\\ILMerge\\ILMerge.exe #{tgt} /wildcards /lib:#{s[:dotnet]} /out:#{s[:bundle]} #{s[:exe]} #{s[:hfm_ref] == '/link' ? '' : "#{HFM_LIB}\\*.dll"} #{s[:log4net]}\\log4net.dll lib\\Interop.SCRIPTINGLib.dll"
end


# Creates a zip file archive containing HFMCmd.exe and the README, LICENSE, and
# HISTORY documents
def package(package_name, *files)
  file_list = files.flatten.map { |f| %Q{"#{Dir.pwd}/#{f}"} }.join(' ')
  "tools\\7za.exe a #{PACKAGE_DIR}/#{package_name}.zip #{file_list}"
end

# ---------

directory BUILD_DIR


# Define a rule for converting .resx files into .resources
rule '.resources' => proc{ |t| "resources/#{t.split('.')[2]}.resx" } do |t|
  sh compile_resource(t.source)
end

# Define resource dependencies on .resx files
desc "Generate resources"
task :resources => BUILD_DIR
RESOURCES.each do |resx|
  file resx
  name = File.basename(resx, '.resx')
  task :resources => "gen/HFMCmd.Resource.#{name}.resources"
end


# Define rule for generating AssemblyInfo.cs
file 'properties/AssemblyInfo.cs.erb'
file 'gen/AssemblyInfo.cs' => 'properties/AssemblyInfo.cs.erb' do
  increment_build
end
task :properties => 'gen/AssemblyInfo.cs'


desc "Updates the build to the current build number"
task :set_build do
  increment_build
end


namespace :dotnet35 do

  directory BUILD35_DIR
  directory RELEASE35_DIR

  # Define .exe dependencies on source files
  file HFMCMD35_EXE => :resources
  file HFMCMD35_EXE => :properties
  SOURCE_FILES.each do |src|
    file HFMCMD35_EXE => src
  end

  file HFMCMD35_EXE => BUILD35_DIR do
    sh compile("3.5")
  end

  file HFMCMD35_BUNDLE => [RELEASE35_DIR, HFMCMD35_EXE] do
    sh bundle("3.5")
  end

  desc "Compile and package HFMCmd using the .NET 3.5 framework"
  task :build => HFMCMD35_BUNDLE

  desc "Create a download package for the .NET 3.5 framework"
  task :package => :build do
    ver = get_version
    sh package "HFMCmd_#{ver}_for_.NET_3.5", HFMCMD35_BUNDLE, PACKAGE_FILES
  end
end


namespace :dotnet40 do

  directory BUILD40_DIR
  directory RELEASE40_DIR

  # Define .exe dependencies on source files
  file HFMCMD40_EXE => :resources
  file HFMCMD40_EXE => :properties
  SOURCE_FILES.each do |src|
    file HFMCMD40_EXE => src
  end

  file HFMCMD40_EXE => BUILD40_DIR do
    sh compile("4.0")
  end

  file HFMCMD40_BUNDLE => [RELEASE40_DIR, HFMCMD40_EXE] do
    sh bundle("4.0")
  end

  desc "Compile and package HFMCmd using the .NET 4.0 framework"
  task :build => HFMCMD40_BUNDLE

  desc "Create a download package for the .NET 4.0 framework"
  task :package => :build do
    ver = get_version
    sh package "HFMCmd_#{ver}_for_.NET_4.0", HFMCMD40_BUNDLE, PACKAGE_FILES
  end
end


desc "Remove all generated files"
task :clean do
  FileUtils.rm_rf BUILD_DIR
  FileUtils.rm_rf RELEASE_DIR
end


desc "Re-generate build.bat, useful for building HFMCmd without Ruby / rake"
task "build.bat" do |t|
  require 'erb'
  template = File.read("build.bat.erb")
  @resources = RESOURCES.map { |res| compile_resource(res) }
  @compile_35 = compile("3.5")
  @compile_40 = compile("4.0")
  @bundle_35 = bundle("3.5")
  @bundle_40 = bundle("4.0")
  erb = ERB.new(template)
  File.open(t.name, "w") do |f|
    f.puts erb.result
  end
end


task :default => 'dotnet35:build'
task :build => [:set_build, 'dotnet35:build', 'dotnet40:build']
task :package => [:build, 'dotnet35:package', 'dotnet40:package']
