// Diff from previous changeset

// The AddFromDirectory method was previously unimplemented.
// See https://stevedower.id.au/blog/new-project-from-existing-code
// for a discussion on why it was added in this changeset.

        /// <summary>
        /// Creates a new project item from an existing directory and all files and subdirectories
        /// contained within it.
        /// </summary>
        /// <param name="directory">The full path of the directory to add.
        /// <returns>A ProjectItem object.</returns>
        public override ProjectItem AddFromDirectory(string directory) {
            CheckProjectIsValid();
            
            ProjectItem result = AddFolder(directory, null);
            
            foreach (string subdirectory in Directory.EnumerateDirectories(directory)) {
                // Assuming this should only import packages
                if (File.Exists(Path.Combine(directory, subdirectory, "__init__.py"))) {
                    result.ProjectItems.AddFromDirectory(Path.Combine(directory, subdirectory));
                }
            }

            foreach (string filename in Directory.EnumerateFiles(directory, "*" + PythonConstants.FileExtension)) {
                result.ProjectItems.AddFromFile(Path.Combine(directory, filename));
            }
            foreach (string filename in Directory.EnumerateFiles(directory, "*" + PythonConstants.WindowsFileExtension))
            {
                result.ProjectItems.AddFromFile(Path.Combine(directory, filename));
            }
            return result;
        }
