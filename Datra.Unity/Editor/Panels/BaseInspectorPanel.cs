using System;
using Datra.Interfaces;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace Datra.Unity.Editor.Panels
{
    public abstract class BaseInspectorPanel : VisualElement
    {
        protected VisualElement headerContainer;
        protected VisualElement breadcrumbContainer;
        protected new VisualElement contentContainer;
        protected Label titleLabel;
        protected Label subtitleLabel;
        
        // Events
        public event Action<Type, bool> OnDataModified;  // Type, isModified
        public event Action<Type, IDataRepository> OnSaveRequested;
        
        protected BaseInspectorPanel()
        {
            Initialize();
        }
        
        private void Initialize()
        {
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;
            
            // Header Section
            headerContainer = new VisualElement();
            headerContainer.AddToClassList("inspector-header");
            
            // Breadcrumb navigation
            breadcrumbContainer = new VisualElement();
            breadcrumbContainer.AddToClassList("breadcrumb-container");
            headerContainer.Add(breadcrumbContainer);
            
            // Title section
            var titleSection = new VisualElement();
            titleSection.AddToClassList("title-section");
            
            titleLabel = new Label();
            titleLabel.AddToClassList("inspector-title");
            titleSection.Add(titleLabel);
            
            subtitleLabel = new Label();
            subtitleLabel.AddToClassList("inspector-subtitle");
            titleSection.Add(subtitleLabel);
            
            headerContainer.Add(titleSection);
            
            // Action buttons in header
            var headerActions = CreateHeaderActions();
            if (headerActions != null)
            {
                headerActions.AddToClassList("header-actions");
                headerContainer.Add(headerActions);
            }
            
            Add(headerContainer);
            
            // Content Section
            contentContainer = new VisualElement();
            contentContainer.AddToClassList("inspector-content");
            contentContainer.style.flexGrow = 1;
            Add(contentContainer);
            
            // Initialize specific panel
            InitializePanel();
            
            // Show empty state initially
            ShowEmptyState();
        }
        
        protected abstract VisualElement CreateHeaderActions();
        protected abstract void InitializePanel();
        
        protected virtual void UpdateHeader(string title, string subtitle = "")
        {
            titleLabel.text = title;
            subtitleLabel.text = subtitle;
        }
        
        protected virtual void ShowEmptyState()
        {
            contentContainer.Clear();
            
            var emptyState = new VisualElement();
            emptyState.AddToClassList("empty-state");
            
            var icon = new VisualElement();
            icon.AddToClassList("empty-state-icon");
            emptyState.Add(icon);
            
            var message = new Label(GetEmptyStateMessage());
            message.AddToClassList("empty-state-message");
            emptyState.Add(message);
            
            contentContainer.Add(emptyState);
            
            titleLabel.text = GetEmptyStateTitle();
            subtitleLabel.text = "";
            breadcrumbContainer.Clear();
        }
        
        protected virtual string GetEmptyStateMessage()
        {
            return "No selection";
        }
        
        protected virtual string GetEmptyStateTitle()
        {
            return "Inspector";
        }
        
        protected void InvokeDataModified(Type type, bool isModified)
        {
            OnDataModified?.Invoke(type, isModified);
        }
        
        protected void InvokeSaveRequested(Type type, IDataRepository repository)
        {
            OnSaveRequested?.Invoke(type, repository);
        }
        
        public virtual void Cleanup()
        {
            // Override in derived classes if cleanup is needed
        }
    }
}