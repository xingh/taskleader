﻿using System;
using System.Configuration;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows.Forms;
using TaskLeader.BLL;
using TaskLeader.BO;
using TaskLeader.DAL;

namespace TaskLeader.GUI
{
    public class TrayIcon : ApplicationContext
    {
        // Déclaration des composants IHM
        private static NotifyIcon trayIcon = new NotifyIcon();
        private ContextMenuStrip trayContext = new ContextMenuStrip();
        private ToolStripMenuItem newActionItem = new ToolStripMenuItem();
        private ToolStripMenuItem outlookItem = new ToolStripMenuItem();
        private ToolStripMenuItem closeItem = new ToolStripMenuItem();
        private ToolStripMenuItem maximItem = new ToolStripMenuItem();

        // Déclaration des composants métiers
        static Control invokeControl = new Control();

        // Gestion des DBs
        /// <summary>
        /// Nom de la DB => Objet DB
        /// </summary>
        public static Dictionary<string, DB> dbs = new Dictionary<string, DB>();
        public static ObservableCollection<string> activeDBs = new ObservableCollection<string>();
        /// <summary>
        /// Liste des filtres affichés dans la Toolbox
        /// </summary>
        public static ObservableCollection<Filtre> displayedFilters = new ObservableCollection<Filtre>();
        public static DB defaultDB
        {
            get
            {
                if(activeDBs.Contains(ConfigurationManager.AppSettings["defaultDB"])) // La DB par défaut n'est pas forcément active
                    return dbs[ConfigurationManager.AppSettings["defaultDB"]]; // Si c'est le cas, elle est la DB par défaut
                else
                    return dbs[activeDBs[0].ToString()]; // Sinon, on prend la première de la liste
            }
        }

        // Déclaration de tous les composants
        private void loadComponents()
        {
            // trayIcon
            trayIcon.ContextMenuStrip = this.trayContext;
            trayIcon.Icon = Properties.Resources.task_coach;
            trayIcon.Text = "TaskLeader v" + Application.ProductVersion; ;
            trayIcon.Visible = true;
            trayIcon.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(displayToolbox);

            // Menu contextuel de la trayIcon
            this.trayContext.Items.AddRange(new ToolStripItem[] { this.newActionItem, this.maximItem, this.outlookItem, this.closeItem });
            this.trayContext.Name = "trayContext";
            this.trayContext.Opened += new EventHandler(trayContext_Opened);

            // Item "nouvelle action" du menu contextuel
            this.newActionItem.Image = TaskLeader.Properties.Resources.add;
            this.newActionItem.Text = "Nouvelle action";
            this.newActionItem.ShowShortcutKeys = true;
            this.newActionItem.ShortcutKeyDisplayString = ((NameValueCollection)ConfigurationManager.GetSection("Hotkey"))["NewAction"];
            this.newActionItem.Click += new System.EventHandler(ajoutAction);

            // Item "afficher Toolbox" du menu contextuel
            this.maximItem.Image = TaskLeader.Properties.Resources.database_table;
            this.maximItem.Text = "Afficher la liste";
            this.maximItem.ShowShortcutKeys = true;
            this.maximItem.ShortcutKeyDisplayString = ((NameValueCollection)ConfigurationManager.GetSection("Hotkey"))["ListeActions"];
            this.maximItem.Click += new System.EventHandler(this.displayToolbox);

            // Item "Connecter à Outlook" du menu contextuel
            this.outlookItem.Text = "Connecter à Outlook";
            this.outlookItem.Click += new System.EventHandler(this.connectOutlook);

            // Item "fermer" du menu contextuel
            this.closeItem.Image = TaskLeader.Properties.Resources.door_out;
            this.closeItem.Text = "Fermer";
            this.closeItem.Click += new System.EventHandler(this.closeItem_Click);
        }

        // Déclaration des hotkeys
        Hotkey hkNewAction = new Hotkey();
        Hotkey hkListe = new Hotkey();

        // Délégué pour les méthodes de raccourcis claviers
        delegate void HotkeyMethodDelegate(object sender, EventArgs e);
        // Récupération du raccourci de la hotkey
        private void registerHotkey(String raccourci, ref Hotkey hotkey, HotkeyMethodDelegate callback)
        {
            //Récupération et ajout des touches spéciales
            if (raccourci.Contains("CTRL"))
                hotkey.Control = true;
            if (raccourci.Contains("ALT"))
                hotkey.Alt = true;
            if (raccourci.Contains("MAJ"))
                hotkey.Shift = true;
            if (raccourci.Contains("WIN"))
                hotkey.Windows = true;

            //Récupération de la lettre
            String lettre = raccourci.Substring(raccourci.LastIndexOf("+") + 1);

            //Si lettre reconnue, ajout de celle-ci et enregistrement de la combinaison
            if (!Enum.IsDefined(typeof(Keys), lettre))
                afficheMessage("Hotkey", "Erreur de formatage du fichier de config");
            else
            {
                hotkey.KeyCode = (Keys)Enum.Parse(typeof(Keys), lettre, false);
                hotkey.Pressed += new System.ComponentModel.HandledEventHandler(callback);

                if (!hotkey.GetCanRegister(invokeControl))
                    afficheMessage("Hotkey", "Impossible d'enregistrer le raccourci");
                else
                    hotkey.Register(invokeControl);
            }
        }

        // Constructeur de la NotifyIcon
        public TrayIcon()
        {
            // On charge tous les composants
            this.loadComponents();

            // Vérification de démarrage
            if (Init.Instance.canLaunch())
            {
                foreach (String dbName in dbs.Keys)
                    activeDBs.Add(dbName); // Au lancement toutes les DBs sont actives
                this.displayToolbox(new Object(), new EventArgs()); // Affichage de la Toolbox
                invokeControl.CreateControl();
            }
            else
            {
                trayIcon.Visible = false;
                Environment.Exit(0);
            }

            // Gestion de l'évènement "Nouveau mail"
            OutlookIF.Instance.NewMail += new NewMailEventHandler(newActionOutlook);

            //Enregistrement des raccourcis clavier
            NameValueCollection section = (NameValueCollection)ConfigurationManager.GetSection("Hotkey");
            registerHotkey(section["NewAction"], ref hkNewAction, new HotkeyMethodDelegate(ajoutAction));
            registerHotkey(section["ListeActions"], ref hkListe, new HotkeyMethodDelegate(displayToolbox));
        }

        private static Toolbox v_toolbox = null;

        // Méthode générique d'affichage de la Toolbox
        private void displayToolbox(object sender, EventArgs e)
        {
            if (v_toolbox == null || v_toolbox.IsDisposed) // Si la fenêtre n'a jamais été ouverte ou fermée, on l'ouvre
            {
                v_toolbox = new Toolbox();
                v_toolbox.WindowState = FormWindowState.Maximized;
                v_toolbox.Show();
            }
            else
                v_toolbox.BringToFront(); // Sinon on l'affiche au premier plan     
        }

        // Méthode permettant d'afficher le formulaire nouvelle action vide
        private static void ajoutAction(object sender, EventArgs e)
        {
            new ManipAction(new TLaction())
                .Show();
        }

        // Méthode appelée sur nouveau mail
        private void newActionOutlook(object sender, NewMailEventArgs e)
        {
            if (invokeControl.InvokeRequired)
                invokeControl.Invoke(new NewMailEventHandler(newActionOutlook), new object[] { sender, e });
            else // Demande d'ajout de mail à une action
            {
                TLaction action = new TLaction();
                action.Texte = e.Mail.Titre;
                action.addPJ(e.Mail);
                new ManipAction(action).Show();
            }
        }

        // Activation si nécessaire de l'item outlook
        private void trayContext_Opened(object sender, EventArgs e)
        {
            this.outlookItem.Visible = OutlookIF.Instance.connectionNeeded;
        }

        // Tentative de connexion à Outlook
        private void connectOutlook(object sender, EventArgs e)
        {
            OutlookIF.Instance.tryHook(false);
        }

        // Méthode générique de fermeture de l'appli
        private void closeApp()
        {
            //Désenregistrement des raccourcis claviers
            if (hkNewAction.Registered)
                hkNewAction.Unregister();
            if (hkListe.Registered)
                hkListe.Unregister();

            trayIcon.Visible = false;
            Application.Exit();
        }

        // Demande de fermeture via le ContextMenuStrip
        private void closeItem_Click(object sender, EventArgs e)
        {
            this.closeApp();
        }

        // Méthode pour affichage de message
        public static void afficheMessage(String titre, String info)
        {
            // Affichage du bilan en tooltip de la tray icon
            trayIcon.ShowBalloonTip(10, titre, info, ToolTipIcon.Info);
        }
    }
}
