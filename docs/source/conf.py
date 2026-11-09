# Configuration file for the Sphinx documentation builder.

# -- Project information

# This documentation is inherited from AutoMapper (MIT, Copyright (c) Jimmy Bogard and contributors).
# ForkForward.Mapper is a community fork of AutoMapper; see NOTICE.md in the repository root.
project = 'ForkForward.Mapper'
copyright = '2010 Jimmy Bogard and AutoMapper contributors; modifications 2026 ForkForward contributors'
author = 'Jimmy Bogard and AutoMapper contributors; ForkForward contributors'

# -- General configuration

extensions = [
    'sphinx.ext.duration',
    'sphinx.ext.doctest',
    'sphinx.ext.autodoc',
    'sphinx.ext.autosummary',
    'sphinx.ext.intersphinx',
    'myst_parser'
]

intersphinx_mapping = {
    'python': ('https://docs.python.org/3/', None),
    'sphinx': ('https://www.sphinx-doc.org/en/master/', None),
}
intersphinx_disabled_domains = ['std']

templates_path = ['_templates']

# -- Options for HTML output

html_theme = 'sphinx_rtd_theme'
html_theme_options = {
    'logo_only': False,
    'display_version': False
}
html_logo = 'img/logo.png'


# -- Options for EPUB output
epub_show_urls = 'footnote'