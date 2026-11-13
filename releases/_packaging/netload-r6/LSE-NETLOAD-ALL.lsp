;;; LSE-NETLOAD-ALL.lsp  -  NETLOADs the five LSE AutoCAD Electrical 2026 plugins in one go.
;;;
;;; Use:  drag this file into the AutoCAD Electrical drawing area, or APPLOAD it.
;;;       It loads every plugin straight away and defines the command LSELOAD to do it again later.
;;;       Add it to the APPLOAD Startup Suite to have the plugins loaded in every session.
;;;
;;; Where it looks for the DLLs: the folder that holds this file when AutoCAD can see it (support path or
;;; current folder), otherwise the fixed root below. Edit the root if you extracted the zip somewhere else.
;;; Each plugin sits in its own sub-folder named after its DLL, with its JSON / standard\ / profiles\ beside it.

(setq *lse-netload-root* "C:\\LSE-Plugins-NETLOAD")

(setq *lse-plugins*
  '("LSE.AutoCADElectrical.CatalogTools"     ; LSECATALOG LSEASSIGNCOMPONENT LSECATALOGADMIN LSECATALOGSETTINGS LSECATALOGDIAG
    "LSE.AutoCADElectrical.ProjectTools"     ; LSENEWPROJECT LSEPROJECTACTIVATE LSEPROJECTSETTINGS LSEPROJECTDIAG
    "LSE_Doctor"                             ; LSEDOCTOR LSEDOCTORSCAN LSEDOCTORSETTINGS LSEDOCTORDIAG
    "LSE.AutoCADElectrical.Librarian"        ; LSELIBRARIAN LSECATALOGADD LSEFOOTPRINTMAP LSELIBRARYAUDIT LSELIBRARYSETTINGS LSELIBRARYDIAG
    "LSE_Env"))                              ; LSEENV LSEENVWIRES LSEENVPROFILES LSEENVSNAPSHOT LSEENVSETTINGS LSEENVDIAG

(defun lse:root ( / f)
  ;; prefer the folder this .lsp lives in, when AutoCAD can find it
  (if (and (setq f (findfile "LSE-NETLOAD-ALL.lsp"))
           (findfile (strcat (vl-filename-directory f) "\\LSE_Env\\LSE_Env.dll")))
    (vl-filename-directory f)
    *lse-netload-root*))

(defun lse:netload (root name / dll)
  (setq dll (strcat root "\\" name "\\" name ".dll"))
  (cond
    ((not (findfile dll))
     (princ (strcat "\n  MISSING  " dll))
     nil)
    (t
     (princ (strcat "\n  NETLOAD  " dll))
     (command "_.NETLOAD" dll)
     t)))

(defun c:LSELOAD ( / root ok)
  (setq root (lse:root) ok 0)
  (princ (strcat "\nLSE plugins: loading from " root))
  (foreach n *lse-plugins*
    (if (lse:netload root n) (setq ok (1+ ok))))
  (princ (strcat "\nLSE plugins: " (itoa ok) " of 5 NETLOADed."))
  (if (< ok 5)
    (princ "\n  A MISSING line means the DLL is not where this file expects it: fix *lse-netload-root* at the top of LSE-NETLOAD-ALL.lsp.")
    (princ "\n  Start with each plugin's ...SETTINGS command, then its ...DIAG command: LSECATALOGSETTINGS LSEPROJECTSETTINGS LSEDOCTORSETTINGS LSELIBRARYSETTINGS LSEENVSETTINGS."))
  (princ))

(vl-load-com)
(c:LSELOAD)
(princ "\nType LSELOAD to NETLOAD the LSE plugins again in a new session.")
(princ)
