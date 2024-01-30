// `npx commit-and-tag-version`

const buildprops = {
  filename: "AudioAlign/AudioAlign.csproj",
  type: "csproj",
};

module.exports = {
  bumpFiles: [buildprops],
  packageFiles: [buildprops],
};
