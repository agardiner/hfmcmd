# Define constants for file locations etc
HFM_LIB = 'lib/hfm-11.1.2.2'
LOG4NET35_LIB   = 'lib\log4net-1.2.11\bin\net\3.5\release'
LOG4NET40_LIB   = 'lib\log4net-1.2.11\bin\net\4.0\release'
FRAMEWORK35_DIR = 'C:\WINDOWS\Microsoft.NET\Framework\v3.5'
FRAMEWORK40_DIR = 'C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319'

RELEASE35_DIR   = 'bin/net/3.5'
HFMCMD35_EXE = "gen/HFMCmd_35.exe"
HFMCMD35_BUNDLE = "#{RELEASE35_DIR}/HFMCmd.exe"

RELEASE40_DIR   = 'bin/net/4.0'
HFMCMD40_EXE = "gen/HFMCmd_40.exe"
HFMCMD40_BUNDLE = "#{RELEASE40_DIR}/HFMCmd.exe"


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


def compile(version)
  s = settings_for_version(version)
  options = "/nologo /target:exe /main:HFMCmd.Launcher /out:#{s[:exe]} /debug /optimize+"
  log4net_ref = "/lib:#{s[:log4net]} /reference:log4net.dll"
  hfm = ["/lib:#{HFM_LIB}"]
  FileList["#{HFM_LIB}/*.dll"].each do |dll|
    hfm << "#{s[:hfm_ref]}:#{File.basename(dll)}"
  end
  resources = FileList['gen/*.resources'].map{ |f| "/resource:#{f.gsub('/', '\\')}" }
  source = "src\\*.cs src\\command\\*.cs src\\commandline\\*.cs src\\yaml\\*.cs src\\hfm\\*.cs gen\\*.cs properties\\*.cs"

  "#{s[:dotnet]}\\csc.exe #{options} #{log4net_ref} #{hfm.join(' ')} #{resources.join(' ')} #{source}"
end


def bundle(version)
  s = settings_for_version(version)

  tgt = version == "3.5" ? '' : "/targetplatform:#{s[:ilmerge]},#{s[:dotnet]}"

  "tools\\ILMerge\\ILMerge.exe #{tgt} /wildcards /lib:#{s[:dotnet]} /out:#{s[:bundle]} #{s[:exe]} #{s[:hfm_ref] == '/link' ? '' : "#{HFM_LIB}\\*.dll"} #{s[:log4net]}\\log4net.dll"
end


# ---------

directory "gen"

# Define a rule for converting .resx files into .resources
rule '.resources' => proc{ |t| "resources/#{t.split('.')[2]}.resx" } do |t|
  name = File.basename(t.source, '.resx')
  sh "tools\\ResGen.exe #{t.source} gen\\HFMCmd.Resource.#{name}.resources /str:cs,HFMCmd.Resource,#{name},gen\\#{name}Resource.cs"
end

# Define resource dependencies on .resx files
desc "Generate resources"
task :resources => "gen"
FileList['resources/*.resx'].each do |resx|
  file resx
  name = File.basename(resx, '.resx')
  task :resources => "gen/HFMCmd.Resource.#{name}.resources"
end



namespace :dotnet35 do

  directory RELEASE35_DIR

  # Define .exe dependencies on source files
  file HFMCMD35_EXE => :resources
  FileList['src/**/*.cs'].each do |src|
    file HFMCMD35_EXE => src
  end

  file HFMCMD35_EXE do
    sh compile("3.5")
  end

  file HFMCMD35_BUNDLE => [RELEASE35_DIR, HFMCMD35_EXE] do
    sh bundle("3.5")
  end

  desc "Compile and package HFMCmd using the .NET 3.5 framework"
  task :build => HFMCMD35_BUNDLE

end


namespace :dotnet40 do

  directory RELEASE40_DIR

  # Define .exe dependencies on source files
  file HFMCMD40_EXE => :resources
  FileList['src/**/*.cs'].each do |src|
    file HFMCMD40_EXE => src
  end

  file HFMCMD40_EXE do
    sh compile("4.0")
  end

  file HFMCMD40_BUNDLE => [RELEASE40_DIR, HFMCMD40_EXE] do
    sh bundle("4.0")
  end

  desc "Compile and package HFMCmd using the .NET 4.0 framework"
  task :build => HFMCMD40_BUNDLE

end

desc "Remove all generated files"
task :clean do
  FileUtils.rm_rf "gen"
  FileUtils.rm_rf "bin"
end

task :default => 'dotnet35:build'
